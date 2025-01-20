namespace CollapseLauncher.Interfaces
{
    internal interface ICacheBase<out T>
    {
        T AsBaseType();
    }
}
