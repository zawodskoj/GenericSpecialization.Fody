using System;

namespace GenericSpecialization
{
    /// <summary>
    /// Forces all methods (including property get/set methods) in class to use specialized class types instead of
    /// generic classes
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public class InjectSpecializationsAttribute : Attribute {}
}