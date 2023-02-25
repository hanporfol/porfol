using System.IO.Pipes;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;

namespace Diff_Library
{
    internal static class CompatibilityExtensions
    {
        // JScript splice function
        public static List<T> Splice<T>(this List<T> input, int start, int count,
            params T[] objects)
        {
            List<T> deletedRange = input.GetRange(start, count);
            input.RemoveRange(start, count);
            input.InsertRange(start, objects);

            return deletedRange;
        }

        // Java substring function
        public static string JavaSubstring(this string s, int begin, int end)
        {
            return s.Substring(begin, end - begin);
        }
    }

    public enum Operation
    {
        DELETE, INSERT, EQUAL
    }


    public class Diff
    {
        public Operation operation;
        public string text;

        public Diff(Operation operation, string text)
        {
            this.operation = operation;
            this.text = text;
        }

        public override string ToString()
        {
            string prettyText = this.text.Replace('\n', '\u00b6');
            string caseDiff = string.Empty;
            switch (this.operation)
            {
                case Operation.DELETE:
                    caseDiff = "<";
                    break;
                case Operation.INSERT:
                    caseDiff = ">";
                    break;
                case Operation.EQUAL:
                    caseDiff = "=";
                    break;
                default:
                    break;
            }
            return caseDiff + "\"" + prettyText + "\"";
        }

        public override bool Equals(Object obj)
        {
            if (obj == null)
            {
                return false;
            }

            Diff p = obj as Diff;
            if ((System.Object)p == null)
            {
                return false;
            }

            return p.operation == this.operation && p.text == this.text;
        }

        public bool Equals(Diff obj)
        {
            if (obj == null)
            {
                return false;
            }

            return obj.operation == this.operation && obj.text == this.text;
        }

        public override int GetHashCode()
        {
            return text.GetHashCode() ^ operation.GetHashCode();
        }
    }


    public class diff_match_patch
    {
        public float Diff_Timeout = 1.0f;
        public short Diff_EditCost = 4;
        public float Match_Threshold = 0.5f;
        public int Match_Distance = 1000;
        public float Patch_DeleteThreshold = 0.5f;
        public short Patch_Margin = 4;
        private short Match_MaxBits = 32;

        private Regex BLANKLINEEND = new Regex("\\n\\r?\\n\\Z");
        private Regex BLANKLINESTART = new Regex("\\A\\r?\\n\\r?\\n");

        public List<Diff> diff_main(string source, string target)
        {
            return diff_main(source, target, true);
        }


        public List<Diff> diff_main(string source, string target, bool checklines)
        {
            DateTime deadline;
            if (this.Diff_Timeout <= 0)
            {
                deadline = DateTime.MaxValue;
            }
            else
            {
                deadline = DateTime.Now +
                    new TimeSpan(((long)(Diff_Timeout * 1000)) * 10000);
            }
            return diff_main(source, target, checklines, deadline);
        }


        private List<Diff> diff_main(string source, string target, bool checklines,
            DateTime deadline)
        {
            List<Diff> diffs;
            if (source == target)
            {
                diffs = new List<Diff>();
                if (source.Length != 0)
                {
                    diffs.Add(new Diff(Operation.EQUAL, source));
                }
                return diffs;
            }

            //공통 접두사 제거
            int commonlength = diff_commonPrefix(source, target);
            string commonprefix = source.Substring(0, commonlength);
            source = source.Substring(commonlength);
            target = target.Substring(commonlength);

            //공통 접미사 제거
            commonlength = diff_commonSuffix(source, target);
            string commonsuffix = source.Substring(source.Length - commonlength);
            source = source.Substring(0, source.Length - commonlength);
            target = target.Substring(0, target.Length - commonlength);

            //중간 블럭 비교
            diffs = diff_compute(source, target, checklines, deadline);

            //접두사 접미사 복원
            if (commonprefix.Length != 0)
            {
                diffs.Insert(0, (new Diff(Operation.EQUAL, commonprefix)));
            }
            if (commonsuffix.Length != 0)
            {
                diffs.Add(new Diff(Operation.EQUAL, commonsuffix));
            }

            diff_cleanupMerge(diffs);
            return diffs;
        }

        private List<Diff> diff_compute(string source, string target,
                                        bool checklines, DateTime deadline)
        {
            List<Diff> diffs = new List<Diff>();

            if (source.Length == 0)
            {
                diffs.Add(new Diff(Operation.INSERT, target));
                return diffs;
            }

            if (target.Length == 0)
            {
                diffs.Add(new Diff(Operation.DELETE, source));
                return diffs;
            }

            string longtext = source.Length > target.Length ? source : target;
            string shorttext = source.Length > target.Length ? target : source;
            int i = longtext.IndexOf(shorttext, StringComparison.Ordinal);
            if (i != -1)
            {
                Operation op = (source.Length > target.Length) ?
                    Operation.DELETE : Operation.INSERT;
                diffs.Add(new Diff(op, longtext.Substring(0, i)));
                diffs.Add(new Diff(Operation.EQUAL, shorttext));
                diffs.Add(new Diff(op, longtext.Substring(i + shorttext.Length)));
                return diffs;
            }

            if (shorttext.Length == 1)
            {
                diffs.Add(new Diff(Operation.DELETE, source));
                diffs.Add(new Diff(Operation.INSERT, target));
                return diffs;
            }

            string[] hm = diff_halfMatch(source, target);
            if (hm != null)
            {
                string text1_a = hm[0];
                string text1_b = hm[1];
                string text2_a = hm[2];
                string text2_b = hm[3];
                string mid_common = hm[4];
                List<Diff> diffs_a = diff_main(text1_a, text2_a, checklines, deadline);
                List<Diff> diffs_b = diff_main(text1_b, text2_b, checklines, deadline);
                diffs = diffs_a;
                diffs.Add(new Diff(Operation.EQUAL, mid_common));
                diffs.AddRange(diffs_b);
                return diffs;
            }

            if (checklines && source.Length > 100 && target.Length > 100)
            {
                return diff_lineMode(source, target, deadline);
            }

            return diff_bisect(source, target, deadline);
        }
        public int diff_commonPrefix(string source, string target)
        {
            int n = Math.Min(source.Length, target.Length);
            for (int i = 0; i < n; i++)
            {
                if (source[i] != target[i])
                {
                    return i;
                }
            }
            return n;
        }

        public int diff_commonSuffix(string source, string target)
        {
            // Performance analysis: https://neil.fraser.name/news/2007/10/09/
            int text1_length = source.Length;
            int text2_length = target.Length;
            int n = Math.Min(source.Length, target.Length);
            for (int i = 1; i <= n; i++)
            {
                if (source[text1_length - i] != target[text2_length - i])
                {
                    return i - 1;
                }
            }
            return n;
        }

        private List<Diff> diff_lineMode(string source, string target,
                                         DateTime deadline)
        {
            Object[] a = diff_linesToChars(source, target);
            source = (string)a[0];
            target = (string)a[1];
            List<string> linearray = (List<string>)a[2];

            List<Diff> diffs = diff_main(source, target, false, deadline);

            diff_charsToLines(diffs, linearray);

            diff_cleanupSemantic(diffs);

            diffs.Add(new Diff(Operation.EQUAL, string.Empty));
            int pointer = 0;
            int count_delete = 0;
            int count_insert = 0;
            string text_delete = string.Empty;
            string text_insert = string.Empty;
            while (pointer < diffs.Count)
            {
                switch (diffs[pointer].operation)
                {
                    case Operation.INSERT:
                        count_insert++;
                        text_insert += diffs[pointer].text;
                        break;
                    case Operation.DELETE:
                        count_delete++;
                        text_delete += diffs[pointer].text;
                        break;
                    case Operation.EQUAL:
                        if (count_delete >= 1 && count_insert >= 1)
                        {
                            diffs.RemoveRange(pointer - count_delete - count_insert,
                                count_delete + count_insert);
                            pointer = pointer - count_delete - count_insert;
                            List<Diff> subDiff =
                                this.diff_main(text_delete, text_insert, false, deadline);
                            diffs.InsertRange(pointer, subDiff);
                            pointer = pointer + subDiff.Count;
                        }
                        count_insert = 0;
                        count_delete = 0;
                        text_delete = string.Empty;
                        text_insert = string.Empty;
                        break;
                }
                pointer++;
            }
            diffs.RemoveAt(diffs.Count - 1);  // Remove the dummy entry at the end.

            return diffs;
        }

        protected List<Diff> diff_bisect(string source, string target,
            DateTime deadline)
        {
            int source_length = source.Length;
            int target_length = target.Length;
            int max_d = (source_length + target_length + 1) / 2;
            int v_offset = max_d;
            int v_length = 2 * max_d;
            int[] v1 = new int[v_length];
            int[] v2 = new int[v_length];
            for (int x = 0; x < v_length; x++)
            {
                v1[x] = -1;
                v2[x] = -1;
            }
            v1[v_offset + 1] = 0;
            v2[v_offset + 1] = 0;
            int delta = source_length - target_length;

            bool front = (delta % 2 != 0);

            int k1start = 0;
            int k1end = 0;
            int k2start = 0;
            int k2end = 0;
            for (int d = 0; d < max_d; d++)
            {
                if (DateTime.Now > deadline)
                {
                    break;
                }

                for (int k1 = -d + k1start; k1 <= d - k1end; k1 += 2)
                {
                    int k1_offset = v_offset + k1;
                    int x1;
                    if (k1 == -d || k1 != d && v1[k1_offset - 1] < v1[k1_offset + 1])
                    {
                        x1 = v1[k1_offset + 1];
                    }
                    else
                    {
                        x1 = v1[k1_offset - 1] + 1;
                    }
                    int y1 = x1 - k1;
                    while (x1 < source_length && y1 < target_length
                          && source[x1] == target[y1])
                    {
                        x1++;
                        y1++;
                    }
                    v1[k1_offset] = x1;
                    if (x1 > source_length)
                    {
                        k1end += 2;
                    }
                    else if (y1 > target_length)
                    {
                        k1start += 2;
                    }
                    else if (front)
                    {
                        int k2_offset = v_offset + delta - k1;
                        if (k2_offset >= 0 && k2_offset < v_length && v2[k2_offset] != -1)
                        {
                            int x2 = source_length - v2[k2_offset];
                            if (x1 >= x2)
                            {
                                return diff_bisectSplit(source, target, x1, y1, deadline);
                            }
                        }
                    }
                }

                for (int k2 = -d + k2start; k2 <= d - k2end; k2 += 2)
                {
                    int k2_offset = v_offset + k2;
                    int x2;
                    if (k2 == -d || k2 != d && v2[k2_offset - 1] < v2[k2_offset + 1])
                    {
                        x2 = v2[k2_offset + 1];
                    }
                    else
                    {
                        x2 = v2[k2_offset - 1] + 1;
                    }
                    int y2 = x2 - k2;
                    while (x2 < source_length && y2 < target_length
                        && source[source_length - x2 - 1]
                        == target[target_length - y2 - 1])
                    {
                        x2++;
                        y2++;
                    }
                    v2[k2_offset] = x2;
                    if (x2 > source_length)
                    {
                        k2end += 2;
                    }
                    else if (y2 > target_length)
                    {
                        k2start += 2;
                    }
                    else if (!front)
                    {
                        int k1_offset = v_offset + delta - k2;
                        if (k1_offset >= 0 && k1_offset < v_length && v1[k1_offset] != -1)
                        {
                            int x1 = v1[k1_offset];
                            int y1 = v_offset + x1 - k1_offset;
                            x2 = source_length - v2[k2_offset];
                            if (x1 >= x2)
                            {
                                return diff_bisectSplit(source, target, x1, y1, deadline);
                            }
                        }
                    }
                }
            }

            List<Diff> diffs = new List<Diff>();
            diffs.Add(new Diff(Operation.DELETE, source));
            diffs.Add(new Diff(Operation.INSERT, target));
            return diffs;
        }

        private List<Diff> diff_bisectSplit(string text1, string text2,
            int x, int y, DateTime deadline)
        {
            string text1a = text1.Substring(0, x);
            string text2a = text2.Substring(0, y);
            string text1b = text1.Substring(x);
            string text2b = text2.Substring(y);

            List<Diff> diffs = diff_main(text1a, text2a, false, deadline);
            List<Diff> diffsb = diff_main(text1b, text2b, false, deadline);

            diffs.AddRange(diffsb);
            return diffs;
        }

        protected Object[] diff_linesToChars(string text1, string text2)
        {
            List<string> lineArray = new List<string>();
            Dictionary<string, int> lineHash = new Dictionary<string, int>();

            lineArray.Add(string.Empty);

            string chars1 = diff_linesToCharsMunge(text1, lineArray, lineHash, 40000);
            string chars2 = diff_linesToCharsMunge(text2, lineArray, lineHash, 65535);
            return new Object[] { chars1, chars2, lineArray };
        }

        private string diff_linesToCharsMunge(string text, List<string> lineArray,
             Dictionary<string, int> lineHash, int maxLines)
        {
            int lineStart = 0;
            int lineEnd = -1;
            string line;
            StringBuilder chars = new StringBuilder();

            while (lineEnd < text.Length - 1)
            {
                lineEnd = text.IndexOf('\n', lineStart);
                if (lineEnd == -1)
                {
                    lineEnd = text.Length - 1;
                }
                line = text.JavaSubstring(lineStart, lineEnd + 1);

                if (lineHash.ContainsKey(line))
                {
                    chars.Append(((char)(int)lineHash[line]));
                }
                else
                {
                    if (lineArray.Count == maxLines)
                    {
                        line = text.Substring(lineStart);
                        lineEnd = text.Length;
                    }
                    lineArray.Add(line);
                    lineHash.Add(line, lineArray.Count - 1);
                    chars.Append(((char)(lineArray.Count - 1)));
                }
                lineStart = lineEnd + 1;
            }
            return chars.ToString();
        }


        protected void diff_charsToLines(ICollection<Diff> diffs,
                        IList<string> lineArray)
        {
            StringBuilder text;
            foreach (Diff diff in diffs)
            {
                text = new StringBuilder();
                for (int j = 0; j < diff.text.Length; j++)
                {
                    text.Append(lineArray[diff.text[j]]);
                }
                diff.text = text.ToString();
            }
        }

        protected int diff_commonOverlap(string text1, string text2)
        {
            int text1_length = text1.Length;
            int text2_length = text2.Length;
            if (text1_length == 0 || text2_length == 0)
            {
                return 0;
            }
            if (text1_length > text2_length)
            {
                text1 = text1.Substring(text1_length - text2_length);
            }
            else if (text1_length < text2_length)
            {
                text2 = text2.Substring(0, text1_length);
            }
            int text_length = Math.Min(text1_length, text2_length);
            if (text1 == text2)
            {
                return text_length;
            }

            int best = 0;
            int length = 1;
            while (true)
            {
                string pattern = text1.Substring(text_length - length);
                int found = text2.IndexOf(pattern, StringComparison.Ordinal);
                if (found == -1)
                {
                    return best;
                }
                length += found;
                if (found == 0 || text1.Substring(text_length - length) ==
                    text2.Substring(0, length))
                {
                    best = length;
                    length++;
                }
            }
        }

        protected string[] diff_halfMatch(string source, string target)
        {
            if (this.Diff_Timeout <= 0)
            {
                return null;
            }
            string longtext = source.Length > target.Length ? source : target;
            string shorttext = source.Length > target.Length ? target : source;
            if (longtext.Length < 4 || shorttext.Length * 2 < longtext.Length)
            {
                return null;
            }

            string[] hm1 = diff_halfMatchI(longtext, shorttext,
                                           (longtext.Length + 3) / 4);
            string[] hm2 = diff_halfMatchI(longtext, shorttext,
                                           (longtext.Length + 1) / 2);
            string[] hm;
            if (hm1 == null && hm2 == null)
            {
                return null;
            }
            else if (hm2 == null)
            {
                hm = hm1;
            }
            else if (hm1 == null)
            {
                hm = hm2;
            }
            else
            {
                hm = hm1[4].Length > hm2[4].Length ? hm1 : hm2;
            }

            if (source.Length > target.Length)
            {
                return hm;
            }
            else
            {
                return new string[] { hm[2], hm[3], hm[0], hm[1], hm[4] };
            }
        }

        private string[] diff_halfMatchI(string longtext, string shorttext, int i)
        {
            string seed = longtext.Substring(i, longtext.Length / 4);
            int j = -1;
            string best_common = string.Empty;
            string best_longtext_a = string.Empty, best_longtext_b = string.Empty;
            string best_shorttext_a = string.Empty, best_shorttext_b = string.Empty;
            while (j < shorttext.Length && (j = shorttext.IndexOf(seed, j + 1,
                StringComparison.Ordinal)) != -1)
            {
                int prefixLength = diff_commonPrefix(longtext.Substring(i),
                                                     shorttext.Substring(j));
                int suffixLength = diff_commonSuffix(longtext.Substring(0, i),
                                                     shorttext.Substring(0, j));
                if (best_common.Length < suffixLength + prefixLength)
                {
                    best_common = shorttext.Substring(j - suffixLength, suffixLength)
                        + shorttext.Substring(j, prefixLength);
                    best_longtext_a = longtext.Substring(0, i - suffixLength);
                    best_longtext_b = longtext.Substring(i + prefixLength);
                    best_shorttext_a = shorttext.Substring(0, j - suffixLength);
                    best_shorttext_b = shorttext.Substring(j + prefixLength);
                }
            }
            if (best_common.Length * 2 >= longtext.Length)
            {
                return new string[]{best_longtext_a, best_longtext_b,
            best_shorttext_a, best_shorttext_b, best_common};
            }
            else
            {
                return null;
            }
        }

        public void diff_cleanupSemantic(List<Diff> diffs)
        {
            bool changes = false;
            Stack<int> equalities = new Stack<int>();
            string lastEquality = null;
            int pointer = 0;
            int length_insertions1 = 0;
            int length_deletions1 = 0;
            int length_insertions2 = 0;
            int length_deletions2 = 0;
            while (pointer < diffs.Count)
            {
                if (diffs[pointer].operation == Operation.EQUAL)
                {
                    equalities.Push(pointer);
                    length_insertions1 = length_insertions2;
                    length_deletions1 = length_deletions2;
                    length_insertions2 = 0;
                    length_deletions2 = 0;
                    lastEquality = diffs[pointer].text;
                }
                else
                {
                    if (diffs[pointer].operation == Operation.INSERT)
                    {
                        length_insertions2 += diffs[pointer].text.Length;
                    }
                    else
                    {
                        length_deletions2 += diffs[pointer].text.Length;
                    }

                    if (lastEquality != null && (lastEquality.Length
                        <= Math.Max(length_insertions1, length_deletions1))
                        && (lastEquality.Length
                            <= Math.Max(length_insertions2, length_deletions2)))
                    {
                        diffs.Insert(equalities.Peek(),
                                     new Diff(Operation.DELETE, lastEquality));
                        diffs[equalities.Peek() + 1].operation = Operation.INSERT;
                        equalities.Pop();
                        if (equalities.Count > 0)
                        {
                            equalities.Pop();
                        }
                        pointer = equalities.Count > 0 ? equalities.Peek() : -1;
                        length_insertions1 = 0;
                        length_deletions1 = 0;
                        length_insertions2 = 0;
                        length_deletions2 = 0;
                        lastEquality = null;
                        changes = true;
                    }
                }
                pointer++;
            }

            // Normalize the diff.
            if (changes)
            {
                diff_cleanupMerge(diffs);
            }
            diff_cleanupSemanticLossless(diffs);

            pointer = 1;
            while (pointer < diffs.Count)
            {
                if (diffs[pointer - 1].operation == Operation.DELETE &&
                    diffs[pointer].operation == Operation.INSERT)
                {
                    string deletion = diffs[pointer - 1].text;
                    string insertion = diffs[pointer].text;
                    int overlap_length1 = diff_commonOverlap(deletion, insertion);
                    int overlap_length2 = diff_commonOverlap(insertion, deletion);
                    if (overlap_length1 >= overlap_length2)
                    {
                        if (overlap_length1 >= deletion.Length / 2.0 ||
                            overlap_length1 >= insertion.Length / 2.0)
                        {
                            diffs.Insert(pointer, new Diff(Operation.EQUAL,
                                insertion.Substring(0, overlap_length1)));
                            diffs[pointer - 1].text =
                                deletion.Substring(0, deletion.Length - overlap_length1);
                            diffs[pointer + 1].text = insertion.Substring(overlap_length1);
                            pointer++;
                        }
                    }
                    else
                    {
                        if (overlap_length2 >= deletion.Length / 2.0 ||
                            overlap_length2 >= insertion.Length / 2.0)
                        {

                            diffs.Insert(pointer, new Diff(Operation.EQUAL,
                                deletion.Substring(0, overlap_length2)));
                            diffs[pointer - 1].operation = Operation.INSERT;
                            diffs[pointer - 1].text =
                                insertion.Substring(0, insertion.Length - overlap_length2);
                            diffs[pointer + 1].operation = Operation.DELETE;
                            diffs[pointer + 1].text = deletion.Substring(overlap_length2);
                            pointer++;
                        }
                    }
                    pointer++;
                }
                pointer++;
            }
        }

        public void diff_cleanupSemanticLossless(List<Diff> diffs)
        {
            int pointer = 1;
            while (pointer < diffs.Count - 1)
            {
                if (diffs[pointer - 1].operation == Operation.EQUAL &&
                  diffs[pointer + 1].operation == Operation.EQUAL)
                {
                    string equality1 = diffs[pointer - 1].text;
                    string edit = diffs[pointer].text;
                    string equality2 = diffs[pointer + 1].text;

                    int commonOffset = this.diff_commonSuffix(equality1, edit);
                    if (commonOffset > 0)
                    {
                        string commonString = edit.Substring(edit.Length - commonOffset);
                        equality1 = equality1.Substring(0, equality1.Length - commonOffset);
                        edit = commonString + edit.Substring(0, edit.Length - commonOffset);
                        equality2 = commonString + equality2;
                    }

                    string bestEquality1 = equality1;
                    string bestEdit = edit;
                    string bestEquality2 = equality2;
                    int bestScore = diff_cleanupSemanticScore(equality1, edit) +
                        diff_cleanupSemanticScore(edit, equality2);
                    while (edit.Length != 0 && equality2.Length != 0
                        && edit[0] == equality2[0])
                    {
                        equality1 += edit[0];
                        edit = edit.Substring(1) + equality2[0];
                        equality2 = equality2.Substring(1);
                        int score = diff_cleanupSemanticScore(equality1, edit) +
                            diff_cleanupSemanticScore(edit, equality2);
                        if (score >= bestScore)
                        {
                            bestScore = score;
                            bestEquality1 = equality1;
                            bestEdit = edit;
                            bestEquality2 = equality2;
                        }
                    }

                    if (diffs[pointer - 1].text != bestEquality1)
                    {
                        if (bestEquality1.Length != 0)
                        {
                            diffs[pointer - 1].text = bestEquality1;
                        }
                        else
                        {
                            diffs.RemoveAt(pointer - 1);
                            pointer--;
                        }
                        diffs[pointer].text = bestEdit;
                        if (bestEquality2.Length != 0)
                        {
                            diffs[pointer + 1].text = bestEquality2;
                        }
                        else
                        {
                            diffs.RemoveAt(pointer + 1);
                            pointer--;
                        }
                    }
                }
                pointer++;
            }
        }

        private int diff_cleanupSemanticScore(string one, string two)
        {
            if (one.Length == 0 || two.Length == 0)
            {
                return 6;
            }

            char char1 = one[one.Length - 1];
            char char2 = two[0];
            bool nonAlphaNumeric1 = !Char.IsLetterOrDigit(char1);
            bool nonAlphaNumeric2 = !Char.IsLetterOrDigit(char2);
            bool whitespace1 = nonAlphaNumeric1 && Char.IsWhiteSpace(char1);
            bool whitespace2 = nonAlphaNumeric2 && Char.IsWhiteSpace(char2);
            bool lineBreak1 = whitespace1 && Char.IsControl(char1);
            bool lineBreak2 = whitespace2 && Char.IsControl(char2);
            bool blankLine1 = lineBreak1 && BLANKLINEEND.IsMatch(one);
            bool blankLine2 = lineBreak2 && BLANKLINESTART.IsMatch(two);

            if (blankLine1 || blankLine2)
            {
                return 5;
            }
            else if (lineBreak1 || lineBreak2)
            {
                return 4;
            }
            else if (nonAlphaNumeric1 && !whitespace1 && whitespace2)
            {
                return 3;
            }
            else if (whitespace1 || whitespace2)
            {
                return 2;
            }
            else if (nonAlphaNumeric1 || nonAlphaNumeric2)
            {
                return 1;
            }
            return 0;
        }

        public void diff_cleanupMerge(List<Diff> diffs)
        {
            diffs.Add(new Diff(Operation.EQUAL, string.Empty));
            int pointer = 0;
            int count_delete = 0;
            int count_insert = 0;
            string text_delete = string.Empty;
            string text_insert = string.Empty;
            int commonlength;
            while (pointer < diffs.Count)
            {
                switch (diffs[pointer].operation)
                {
                    case Operation.INSERT:
                        count_insert++;
                        text_insert += diffs[pointer].text;
                        pointer++;
                        break;
                    case Operation.DELETE:
                        count_delete++;
                        text_delete += diffs[pointer].text;
                        pointer++;
                        break;
                    case Operation.EQUAL:
                        if (count_delete + count_insert > 1)
                        {
                            if (count_delete != 0 && count_insert != 0)
                            {
                                commonlength = this.diff_commonPrefix(text_insert, text_delete);
                                if (commonlength != 0)
                                {
                                    if ((pointer - count_delete - count_insert) > 0 &&
                                      diffs[pointer - count_delete - count_insert - 1].operation
                                          == Operation.EQUAL)
                                    {
                                        diffs[pointer - count_delete - count_insert - 1].text
                                            += text_insert.Substring(0, commonlength);
                                    }
                                    else
                                    {
                                        diffs.Insert(0, new Diff(Operation.EQUAL,
                                            text_insert.Substring(0, commonlength)));
                                        pointer++;
                                    }
                                    text_insert = text_insert.Substring(commonlength);
                                    text_delete = text_delete.Substring(commonlength);
                                }
                                commonlength = this.diff_commonSuffix(text_insert, text_delete);
                                if (commonlength != 0)
                                {
                                    diffs[pointer].text = text_insert.Substring(text_insert.Length
                                        - commonlength) + diffs[pointer].text;
                                    text_insert = text_insert.Substring(0, text_insert.Length
                                        - commonlength);
                                    text_delete = text_delete.Substring(0, text_delete.Length
                                        - commonlength);
                                }
                            }
                            pointer -= count_delete + count_insert;
                            diffs.Splice(pointer, count_delete + count_insert);
                            if (text_delete.Length != 0)
                            {
                                diffs.Splice(pointer, 0,
                                    new Diff(Operation.DELETE, text_delete));
                                pointer++;
                            }
                            if (text_insert.Length != 0)
                            {
                                diffs.Splice(pointer, 0,
                                    new Diff(Operation.INSERT, text_insert));
                                pointer++;
                            }
                            pointer++;
                        }
                        else if (pointer != 0
                            && diffs[pointer - 1].operation == Operation.EQUAL)
                        {
                            diffs[pointer - 1].text += diffs[pointer].text;
                            diffs.RemoveAt(pointer);
                        }
                        else
                        {
                            pointer++;
                        }
                        count_insert = 0;
                        count_delete = 0;
                        text_delete = string.Empty;
                        text_insert = string.Empty;
                        break;
                }
            }
            if (diffs[diffs.Count - 1].text.Length == 0)
            {
                diffs.RemoveAt(diffs.Count - 1);
            }

            bool changes = false;
            pointer = 1;

            while (pointer < (diffs.Count - 1))
            {
                if (diffs[pointer - 1].operation == Operation.EQUAL &&
                  diffs[pointer + 1].operation == Operation.EQUAL)
                {
                    if (diffs[pointer].text.EndsWith(diffs[pointer - 1].text,
                        StringComparison.Ordinal))
                    {
                        diffs[pointer].text = diffs[pointer - 1].text +
                            diffs[pointer].text.Substring(0, diffs[pointer].text.Length -
                                                          diffs[pointer - 1].text.Length);
                        diffs[pointer + 1].text = diffs[pointer - 1].text
                            + diffs[pointer + 1].text;
                        diffs.Splice(pointer - 1, 1);
                        changes = true;
                    }
                    else if (diffs[pointer].text.StartsWith(diffs[pointer + 1].text,
                        StringComparison.Ordinal))
                    {
                        diffs[pointer - 1].text += diffs[pointer + 1].text;
                        diffs[pointer].text =
                            diffs[pointer].text.Substring(diffs[pointer + 1].text.Length)
                            + diffs[pointer + 1].text;
                        diffs.Splice(pointer + 1, 1);
                        changes = true;
                    }
                }
                pointer++;
            }

            if (changes)
            {
                this.diff_cleanupMerge(diffs);
            }
        }
    }
}