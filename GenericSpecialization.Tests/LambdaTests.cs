using System;
using System.Collections.Generic;
using System.IO;
using Fody;
using Xunit;

namespace GenericSpecialization.Tests
{
    public class LambdaTests
    {
        public static IEnumerable<object[]> Instances;

        static LambdaTests()
        {
            Instances = GenericSpecializationTests.LambdaTestInstances;
        }

        [Theory, MemberData(nameof(Instances))]
        public void JoinSelectStringsToStrings(dynamic instance)
        {
            Func<string, string> selector = x => x + 1;
            Assert.Equal("foo1, bar1, baz1", instance.JoinSelectStrings<string>(", ", new[] { "foo", "bar", "baz" }, selector));
        }
        
        [Theory, MemberData(nameof(Instances))]
        public void JoinSelectStringsToInts(dynamic instance)
        {
            Func<string, int> selector = x => int.Parse(x) + 1;
            Assert.Equal("4, 5, 6", instance.JoinSelectStrings<int>(", ", new[] { "3", "4", "5" }, selector));
        }
        
        [Theory, MemberData(nameof(Instances))]
        public void JoinSelectIntsToInts(dynamic instance)
        {
            Func<int, int> selector = x => x + 1;
            Assert.Equal("2, 3, 4", instance.JoinSelectInts<int>(", ", new[] { 1, 2, 3 }, selector));
        }
        
        [Theory, MemberData(nameof(Instances))]
        public void JoinSelectIntsToStrings(dynamic instance)
        {
            Func<int, string> selector = x => (x + 1).ToString();
            Assert.Equal("2, 3, 4", instance.JoinSelectInts<string>(", ", new[] { 1, 2, 3 }, selector));
        }
    }
}