using System.Threading;

namespace Simple
{
    class Program
    {
        static void Main(string[] args)
        {
            NickBot bot = new NickBot();


            while (!bot.BotQuit)
            {

                bot.Update();

                //run at 60Hz
                Thread.Sleep(16);

            }
        }
    }
}
