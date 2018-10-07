using System;
using System.Threading;

namespace Simple
{
    class Program
    {
        static void Main(string[] args)
        {
            NickBot bot = new NickBot();

            try
            {
                while (!bot.BotQuit)
                {

                    bot.Update();

                    //run at 60Hz
                    Thread.Sleep(32);

                }
            }
            catch(Exception ex)
            {
                Console.WriteLine(ex.ToString());
               
            }

            Console.ReadLine();
        }
    }
}
