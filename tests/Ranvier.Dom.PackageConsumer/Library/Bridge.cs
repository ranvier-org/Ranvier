public static class Bridge
{
    public static System.Type NodeContract => typeof(Ranvier.Dom.Node);
    public static System.Type TableContract => typeof(Ranvier.Dom.HTMLTableElement);
    public static System.Type StyleSheetServiceContract => typeof(Ranvier.Dom.StyleSheetService);
    public static Ranvier.Dom.Node Identity(Ranvier.Dom.Node value) => value;
}
