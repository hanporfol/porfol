namespace Diff_Library
{
    internal class Program
    {
        static void Main(string[] args)
        {
            //if (args.Length < 2) return;
            Console.WriteLine("소스 파일 경로를 입력해주세요.");
            string source_Path = Console.ReadLine();
            if (!File.Exists(source_Path))
            {
                Console.WriteLine("소스 파일의 경로를 확인해주세요.");
                return;
            }
            
            Console.WriteLine("타겟 파일 경로를 입력해주세요.");
            string target_Path = Console.ReadLine();
            if (!File.Exists(target_Path))
            {
                Console.WriteLine("타겟 파일읙 경로를 확인해주세요.");
                return;
            }

            string source = System.IO.File.ReadAllText(source_Path);
            string tartget = System.IO.File.ReadAllText(target_Path);
           
            diff_match_patch diff = new(); // 객체 생성            
            var output = diff.diff_main(source, tartget);      // 파일 비교
            Console.WriteLine(string.Join("\n", output));   // 비교 내용 출력
            
        }
    }
}