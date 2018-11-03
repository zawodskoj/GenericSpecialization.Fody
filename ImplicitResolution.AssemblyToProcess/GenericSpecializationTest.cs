using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using GenericSpecialization.Fody;
using ImplicitResolution.Fody;

namespace ImplicitResolution.AssemblyToProcess
{
    [GenerateSpecialization(typeof(int))]
    [GenerateSpecialization(typeof(string))]
    public class GenericClass<T>
    {
        public void Method_AcceptsT(T t) {}
        public T Method_AcceptsT_ReturnsT(T t) => t;
        public bool Method_AcceptsTwoT_ReturnsEquality(T t1, T t2) => t1.Equals(t2);
    }
    
    [GenerateSpecialization(typeof(int))]
    public class GenericStructuralClass<T> where T : struct, IEquatable<T>
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
        
        public void Method_AcceptsInt() 
            => new GenericClass<int>().Method_AcceptsT(1337);
        public int Method_AcceptsInt_ReturnsInt(int s) 
            => new GenericClass<int>().Method_AcceptsT_ReturnsT(s);
        public bool Method_AcceptsTwoInts_ReturnsEquality(int s1, int s2) 
            => new GenericClass<int>().Method_AcceptsTwoT_ReturnsEquality(s1, s2);
        
        public int StructuralMethod_AcceptsInt_ReturnsInt(int s) 
            => new GenericStructuralClass<int>().Method_AcceptsT_ReturnsT(s);
        public bool StructuralMethod_AcceptsTwoInts_ReturnsEquality(int s1, int s2) 
            => new GenericStructuralClass<int>().Method_AcceptsTwoT_ReturnsEquality(s1, s2);
    }
    
    public class GenericSpecializationTest_NotSpecialized
    {
        public void Method_AcceptsString() 
            => new GenericClass<string>().Method_AcceptsT("Test");
        public string Method_AcceptsString_ReturnsString(string s) 
            => new GenericClass<string>().Method_AcceptsT_ReturnsT(s);
        public bool Method_AcceptsTwoStrings_ReturnsEquality(string s1, string s2) 
            => new GenericClass<string>().Method_AcceptsTwoT_ReturnsEquality(s1, s2);
        
        public void Method_AcceptsInt() 
            => new GenericClass<int>().Method_AcceptsT(1337);
        public int Method_AcceptsInt_ReturnsInt(int s) 
            => new GenericClass<int>().Method_AcceptsT_ReturnsT(s);
        public bool Method_AcceptsTwoInts_ReturnsEquality(int s1, int s2) 
            => new GenericClass<int>().Method_AcceptsTwoT_ReturnsEquality(s1, s2);
        
        public int StructuralMethod_AcceptsInt_ReturnsInt(int s) 
            => new GenericStructuralClass<int>().Method_AcceptsT_ReturnsT(s);
        public bool StructuralMethod_AcceptsTwoInts_ReturnsEquality(int s1, int s2) 
            => new GenericStructuralClass<int>().Method_AcceptsTwoT_ReturnsEquality(s1, s2);
    }
}