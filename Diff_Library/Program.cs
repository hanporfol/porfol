namespace Diff_Library
{
    internal class Program
    {
        static void Main(string[] args)
        {
            if (args.Length < 2) return;

            if (!File.Exists(args[0]))
            {
                Console.WriteLine("첫 번째 경로의 파일을 확인해주세요.");
                return;
            }

            if (!File.Exists(args[1]))
            {
                Console.WriteLine("두 번째 경로의 파일을 확인해주세요.");
                return;
            }

            string sSource = System.IO.File.ReadAllText(args[0]);
            string sTartget = System.IO.File.ReadAllText(args[1]);

            diff_match_patch diff = new diff_match_patch(); // 객체 생성
            var output = diff.diff_main(sSource, sTartget);      // 파일 비교
            Console.WriteLine(string.Join("\n", output));   // 비교 내용 출력
            
        }
    }
}