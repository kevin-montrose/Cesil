namespace Cesil
{
    internal enum BackingMode : byte
    {
        None = 0,

        Method,
        Field,
        Delegate,
        Constructor,
        ConstructorParameter
    }
}
