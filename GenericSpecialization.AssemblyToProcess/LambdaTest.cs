using System;
using System.Collections.Generic;
using System.Linq;

namespace GenericSpecialization.AssemblyToProcess
{
    //[GenerateSpecialization(typeof(int))]
    //[GenerateSpecialization(typeof(string))]
    public class GenericClassWithLambda<T>
    {
        public string JoinSelect<T2>(string s, IEnumerable<T> items,
            Func<T, T2> selector) => string.Join(s, items.Select(x => selector(x)));
    }
    
    // [InjectSpecializations]
    public class LambdaTest_Specialized
    {
        public string JoinSelectInts<T2>(string s, IEnumerable<int> ints, Func<int, T2> selector)
            => new GenericClassWithLambda<int>().JoinSelect(s, ints, selector);
        public string JoinSelectStrings<T2>(string s, IEnumerable<string> strings, Func<string, T2> selector)
            => new GenericClassWithLambda<string>().JoinSelect(s, strings, selector);
    }
    
    public class LambdaTest_NotSpecialized
    {
        public string JoinSelectInts<T2>(string s, IEnumerable<int> ints, Func<int, T2> selector)
            => new GenericClassWithLambda<int>().JoinSelect(s, ints, selector);
        public string JoinSelectStrings<T2>(string s, IEnumerable<string> strings, Func<string, T2> selector)
            => new GenericClassWithLambda<string>().JoinSelect(s, strings, selector);
    }
}