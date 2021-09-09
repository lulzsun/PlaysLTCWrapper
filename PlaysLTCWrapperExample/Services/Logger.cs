using System;

namespace PlaysLTCWrapper.Example.Services {
    public class Logger {
        public static void WriteLine(ConsoleColor titleColor, string title, string message) {
            Console.ForegroundColor = titleColor;
            Console.Write(title);
            Console.ResetColor();
            Console.WriteLine(message);
        }
    }
}
