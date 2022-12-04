

namespace Muddler;


class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("Start");

        var task1 = Task.Run(async () => {
            Console.WriteLine("Started Task 1");

            while (true)
            {
                Console.WriteLine("HEllo from Task 1");

               await Task.Delay(1000);
            }

        });
        var task2 = Task.Run(async() => {
            Console.WriteLine("Started Task 2");

            while (true)
            {
                Console.WriteLine("HEllo from Task 2");

                await Task.Delay(1000);
            }
        });
         

        Task.WaitAll(new[] { task1, task2 });

        Console.ReadKey();
    }



}