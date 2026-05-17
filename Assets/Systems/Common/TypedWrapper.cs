namespace Systems.Common
{ 
    /// <summary>
    /// A base class used for selective resolution in the dependency injection container. Avoids same-type conflicts by wrapping a value in a unique type.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class TypedWrapper<T>
    {
        private readonly T _value;

        protected TypedWrapper(T value)
        {
            _value = value;
        }

        public static implicit operator T(TypedWrapper<T> wrapper) => wrapper._value;
    }
}