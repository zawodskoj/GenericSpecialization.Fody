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

        public override string Show() => That;
    }
    
    public class IntShowable : Showable<int>
    {
        public IntShowable(int that) : base(that) { }

        public override string Show() => That.ToString();
    }
    
    public class Test
    {
        public bool Run()
        {
            var strv = "Hello!";
            var intv = 1337;
            
            return "Hello!" == Implicitly.Resolve<string, Showable<string>>(strv).Show() &&
                   "1337" == Implicitly.Resolve<int, Showable<int>>(intv).Show();
        }
    }
}