using System;
using GenericSpecialization.Fody;

namespace ImplicitResolution.AssemblyToProcess
{
    [GenerateSpecialization(typeof(int))]
    [GenerateSpecialization(typeof(string))]
    public class GenericClass<T>
    {
        public void Method_AcceptsT(T t) {}
        public T Method_AcceptsT_ReturnsT(T t) => t;
        public T Method_AcceptsOverlappedT_ReturnsT<T>(T t) => t;
        public bool Method_AcceptsTwoT_ReturnsEquality(T t1, T t2) => t1.Equals(t2);
        
        public bool Method_AcceptsTAndT2_ReturnsEquality<T2>(T t1, T2 t2) => t1.Equals(t2);
    }

    [GenerateSpecialization(typeof(int))]
    [GenerateSpecialization(typeof(string))]
    public class GenericClassWithNestedClasses<T>
    {
        public class NestedClass
        {
            public T Method_AcceptsT_ReturnsT(T t) => t;
            public T Method_AcceptsOverlappedT_ReturnsT<T>(T t) => t;
            public bool Method_AcceptsTwoT_ReturnsEquality(T t1, T t2) => t1.Equals(t2);
        }

        [GenerateSpecialization(typeof(int))]
        [GenerateSpecialization(typeof(string))]
        public class NestedGenericClassNonOverlapping<T2>
        {
            public T Method_AcceptsT_ReturnsT(T t) => t;
            public T2 Method_AcceptsT2_ReturnsT2(T2 t) => t;
            public T Method_AcceptsOverlappedT_ReturnsT<T>(T t) => t;
            public bool Method_AcceptsTwoT_ReturnsEquality(T t1, T t2) => t1.Equals(t2);
            public bool Method_AcceptsTAndT2_ReturnsEquality(T t1, T2 t2) => t1.Equals(t2);
        }

        [GenerateSpecialization(typeof(int))]
        [GenerateSpecialization(typeof(string))]
        public class NestedGenericClassOverlapping<T>
        {
            public T Method_AcceptsT_ReturnsT(T t) => t;
            public T Method_AcceptsOverlappedT_ReturnsT<T>(T t) => t;
            public bool Method_AcceptsTwoT_ReturnsEquality(T t1, T t2) => t1.Equals(t2);
        }
    }
    
    [GenerateSpecialization(typeof(int))]
    public struct GenericStructuralClass<T> where T : struct, IEquatable<T>
    {
        public T Method_AcceptsT_ReturnsT(T t) => t;
        public bool Method_AcceptsTwoT_ReturnsEquality(T t1, T t2) => t1.Equals(t2);
    }
    
    [InjectSpecializations]
    public class GenericSpecializationTest_Specialized
    {
        public void Method_AcceptsString() 
            => new GenericClass<string>().Method_AcceptsT("Test");
        public string Method_AcceptsString_ReturnsString(string s) 
            => new GenericClass<string>().Method_AcceptsT_ReturnsT(s);
        public bool Method_AcceptsTwoStrings_ReturnsEquality(string s1, string s2) 
            => new GenericClass<string>().Method_AcceptsTwoT_ReturnsEquality(s1, s2);
        public bool Method_AcceptsStringAndT_ReturnsEquality<T>(string s1, T s2) 
            => new GenericClass<string>().Method_AcceptsTAndT2_ReturnsEquality(s1, s2);

        public void Method_AcceptsInt() 
            => new GenericClass<int>().Method_AcceptsT(1337);
        public int Method_AcceptsInt_ReturnsInt(int s) 
            => new GenericClass<int>().Method_AcceptsT_ReturnsT(s);
        public bool Method_AcceptsTwoInts_ReturnsEquality(int s1, int s2) 
            => new GenericClass<int>().Method_AcceptsTwoT_ReturnsEquality(s1, s2);
        public bool Method_AcceptsIntAndT_ReturnsEquality<T>(int s1, T s2) 
            => new GenericClass<int>().Method_AcceptsTAndT2_ReturnsEquality(s1, s2);
        
        public int StructuralMethod_AcceptsInt_ReturnsInt(int s) 
            => new GenericStructuralClass<int>().Method_AcceptsT_ReturnsT(s);
        public bool StructuralMethod_AcceptsTwoInts_ReturnsEquality(int s1, int s2) 
            => new GenericStructuralClass<int>().Method_AcceptsTwoT_ReturnsEquality(s1, s2);
        
        public int NestedMethod_AcceptsInt_ReturnsInt(int s)
            => new GenericClassWithNestedClasses<int>.NestedClass().Method_AcceptsT_ReturnsT(s);
        public int NestedMethod_AcceptsOverlappedInt_ReturnsInt(int s)
            => new GenericClassWithNestedClasses<int>.NestedClass().Method_AcceptsOverlappedT_ReturnsT(s);
        public string NestedMethod_AcceptsOverlappedString_ReturnsString(string s)
            => new GenericClassWithNestedClasses<int>.NestedClass().Method_AcceptsOverlappedT_ReturnsT(s);
        public bool NestedMethod_AcceptsTwoInts_ReturnsEquality(int a, int b)
            => new GenericClassWithNestedClasses<int>.NestedClass().Method_AcceptsTwoT_ReturnsEquality(a, b);
        
        public int NestedMethodWithOverlappedT_AcceptsInt_ReturnsInt(int s)
            => new GenericClassWithNestedClasses<string>.NestedGenericClassOverlapping<int>().Method_AcceptsT_ReturnsT(s);
        public int NestedMethodWithOverlappedT_AcceptsOverlappedInt_ReturnsInt(int s)
            => new GenericClassWithNestedClasses<string>.NestedGenericClassOverlapping<int>().Method_AcceptsOverlappedT_ReturnsT(s);
        public string NestedMethodWithOverlappedT_AcceptsOverlappedString_ReturnsString(string s)
            => new GenericClassWithNestedClasses<string>.NestedGenericClassOverlapping<int>().Method_AcceptsOverlappedT_ReturnsT(s);
        public bool NestedMethodWithOverlappedT_AcceptsTwoInts_ReturnsEquality(int a, int b)
            => new GenericClassWithNestedClasses<string>.NestedGenericClassOverlapping<int>().Method_AcceptsTwoT_ReturnsEquality(a, b);
        
        public string NestedMethodWithNonOverlappedT_AcceptsString_ReturnsString(string s)
            => new GenericClassWithNestedClasses<string>.NestedGenericClassNonOverlapping<int>().Method_AcceptsT_ReturnsT(s);
        public int NestedMethodWithNonOverlappedT_AcceptsOverlappedInt_ReturnsInt(int s)
            => new GenericClassWithNestedClasses<string>.NestedGenericClassNonOverlapping<int>().Method_AcceptsOverlappedT_ReturnsT(s);
        public string NestedMethodWithNonOverlappedT_AcceptsOverlappedString_ReturnsString(string s)
            => new GenericClassWithNestedClasses<string>.NestedGenericClassNonOverlapping<int>().Method_AcceptsOverlappedT_ReturnsT(s);
        public bool NestedMethodWithNonOverlappedT_AcceptsTwoStrings_ReturnsEquality(string a, string b)
            => new GenericClassWithNestedClasses<string>.NestedGenericClassNonOverlapping<int>().Method_AcceptsTwoT_ReturnsEquality(a, b);
        public bool NestedMethodWithNonOverlappedT_AcceptsStringAndInt_ReturnsEquality(string a, int b)
            => new GenericClassWithNestedClasses<string>.NestedGenericClassNonOverlapping<int>().Method_AcceptsTAndT2_ReturnsEquality(a, b);
        public bool NestedMethodWithNonOverlappedT_AcceptsTwoStringsAsDifferentGenerics_ReturnsEquality(string a, string b)
            => new GenericClassWithNestedClasses<string>.NestedGenericClassNonOverlapping<string>().Method_AcceptsTAndT2_ReturnsEquality(a, b);
        public int NestedMethodWithNonOverlappedT_AcceptsInt_ReturnsInt(int s)
            => new GenericClassWithNestedClasses<string>.NestedGenericClassNonOverlapping<int>().Method_AcceptsT2_ReturnsT2(s);
    }
    
    public class GenericSpecializationTest_NotSpecialized
    {
        public void Method_AcceptsString() 
            => new GenericClass<string>().Method_AcceptsT("Test");
        public string Method_AcceptsString_ReturnsString(string s) 
            => new GenericClass<string>().Method_AcceptsT_ReturnsT(s);
        public bool Method_AcceptsTwoStrings_ReturnsEquality(string s1, string s2) 
            => new GenericClass<string>().Method_AcceptsTwoT_ReturnsEquality(s1, s2);
        public bool Method_AcceptsStringAndT_ReturnsEquality<T>(string s1, T s2) 
            => new GenericClass<string>().Method_AcceptsTAndT2_ReturnsEquality(s1, s2);
        
        public void Method_AcceptsInt() 
            => new GenericClass<int>().Method_AcceptsT(1337);
        public int Method_AcceptsInt_ReturnsInt(int s) 
            => new GenericClass<int>().Method_AcceptsT_ReturnsT(s);
        public bool Method_AcceptsTwoInts_ReturnsEquality(int s1, int s2) 
            => new GenericClass<int>().Method_AcceptsTwoT_ReturnsEquality(s1, s2);
        public bool Method_AcceptsIntAndT_ReturnsEquality<T>(int s1, T s2) 
            => new GenericClass<int>().Method_AcceptsTAndT2_ReturnsEquality(s1, s2);
        
        public int StructuralMethod_AcceptsInt_ReturnsInt(int s) 
            => new GenericStructuralClass<int>().Method_AcceptsT_ReturnsT(s);
        public bool StructuralMethod_AcceptsTwoInts_ReturnsEquality(int s1, int s2) 
            => new GenericStructuralClass<int>().Method_AcceptsTwoT_ReturnsEquality(s1, s2);
        
        public int NestedMethod_AcceptsInt_ReturnsInt(int s)
            => new GenericClassWithNestedClasses<int>.NestedClass().Method_AcceptsT_ReturnsT(s);
        public int NestedMethod_AcceptsOverlappedInt_ReturnsInt(int s)
            => new GenericClassWithNestedClasses<int>.NestedClass().Method_AcceptsOverlappedT_ReturnsT(s);
        public string NestedMethod_AcceptsOverlappedString_ReturnsString(string s)
            => new GenericClassWithNestedClasses<int>.NestedClass().Method_AcceptsOverlappedT_ReturnsT(s);
        public bool NestedMethod_AcceptsTwoInts_ReturnsEquality(int a, int b)
            => new GenericClassWithNestedClasses<int>.NestedClass().Method_AcceptsTwoT_ReturnsEquality(a, b);
        
        public int NestedMethodWithOverlappedT_AcceptsInt_ReturnsInt(int s)
            => new GenericClassWithNestedClasses<string>.NestedGenericClassOverlapping<int>().Method_AcceptsT_ReturnsT(s);
        public int NestedMethodWithOverlappedT_AcceptsOverlappedInt_ReturnsInt(int s)
            => new GenericClassWithNestedClasses<string>.NestedGenericClassOverlapping<int>().Method_AcceptsOverlappedT_ReturnsT(s);
        public string NestedMethodWithOverlappedT_AcceptsOverlappedString_ReturnsString(string s)
            => new GenericClassWithNestedClasses<string>.NestedGenericClassOverlapping<int>().Method_AcceptsOverlappedT_ReturnsT(s);
        public bool NestedMethodWithOverlappedT_AcceptsTwoInts_ReturnsEquality(int a, int b)
            => new GenericClassWithNestedClasses<string>.NestedGenericClassOverlapping<int>().Method_AcceptsTwoT_ReturnsEquality(a, b);
        
        public string NestedMethodWithNonOverlappedT_AcceptsString_ReturnsString(string s)
            => new GenericClassWithNestedClasses<string>.NestedGenericClassNonOverlapping<int>().Method_AcceptsT_ReturnsT(s);
        public int NestedMethodWithNonOverlappedT_AcceptsOverlappedInt_ReturnsInt(int s)
            => new GenericClassWithNestedClasses<string>.NestedGenericClassNonOverlapping<int>().Method_AcceptsOverlappedT_ReturnsT(s);
        public string NestedMethodWithNonOverlappedT_AcceptsOverlappedString_ReturnsString(string s)
            => new GenericClassWithNestedClasses<string>.NestedGenericClassNonOverlapping<int>().Method_AcceptsOverlappedT_ReturnsT(s);
        public bool NestedMethodWithNonOverlappedT_AcceptsTwoStrings_ReturnsEquality(string a, string b)
            => new GenericClassWithNestedClasses<string>.NestedGenericClassNonOverlapping<int>().Method_AcceptsTwoT_ReturnsEquality(a, b);
        public bool NestedMethodWithNonOverlappedT_AcceptsStringAndInt_ReturnsEquality(string a, int b)
            => new GenericClassWithNestedClasses<string>.NestedGenericClassNonOverlapping<int>().Method_AcceptsTAndT2_ReturnsEquality(a, b);
        public bool NestedMethodWithNonOverlappedT_AcceptsTwoStringsAsDifferentGenerics_ReturnsEquality(string a, string b)
            => new GenericClassWithNestedClasses<string>.NestedGenericClassNonOverlapping<string>().Method_AcceptsTAndT2_ReturnsEquality(a, b);
        public int NestedMethodWithNonOverlappedT_AcceptsInt_ReturnsInt(int s)
            => new GenericClassWithNestedClasses<string>.NestedGenericClassNonOverlapping<int>().Method_AcceptsT2_ReturnsT2(s);
    }
}