using System;
using ImplicitResolution.Fody;

namespace ImplicitResolution
{
    class Program
    {
        static void Main(string[] args)
        {
            var strv = "Hello!";
            var intv = 1337;
            
            Console.WriteLine(Implicitly.Resolve<string, Showable<string>>(strv).Show());
            Console.WriteLine(Implicitly.Resolve<int, Showable<int>>(intv).Show());
        }
    }

}