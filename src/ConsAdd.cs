namespace LanguageExt.RefParsec;

internal sealed class ConsAdd
{
    public ConsAdd(ushort cons, ushort add)
    {
        CanCons = cons;
        CanAdd = add;
    }

    public ushort CanCons;
    public ushort CanAdd;
}