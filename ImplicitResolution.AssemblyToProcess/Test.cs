using System;
using System.Collections.Generic;
using System.Linq;
using ImplicitResolution.Fody;

namespace ImplicitResolution.AssemblyToProcess
{
    public abstract class Showable<T> : Typeclass<T>
    {
        protected Showable(T that) : base(that) { }

        public abstract string Show();
    }

    public class StrShowable : Showable<string>
    {
        public StrShowable(string that) : base(that) { }

        public override string Show() => $"\"{That}\"";
    }
    
    public class IntShowable : Showable<int>
    {
        public IntShowable(int that) : base(that) { }

        public override string Show() => That.ToString();
    }

    public class ListShowable<T> : Showable<List<T>>
    {
        public ListShowable(List<T> that) : base(that) {}
    
        public override string Show() => $"[{string.Join(", ", That.Select(x => Implicitly.Resolve<Showable<T>>(x).Show()))}]";
    }
        
    public class Test
    {
        public bool Run()
        {
            var strv = "Hello!";
            var intv = 1337;

            return "\"Hello!\"" == Implicitly.Resolve<Showable<string>>(strv).Show() &&
                   "1337" == Implicitly.Resolve<Showable<int>>(intv).Show();
        }
        
        public bool RunNested()
        {
            var listv = new List<string> {"foo", "bar", "baz"};

            return "[\"foo\", \"bar\", \"baz\"]" == Implicitly.Resolve<Showable<List<string>>>(listv).Show();
        }
    }
}