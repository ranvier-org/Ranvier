public static class Bridge
{
    public static System.Type NodeContract => typeof(Ranvier.Dom.Node);
    public static System.Type TableContract => typeof(Ranvier.Dom.HTMLTableElement);
    public static System.Type StyleSheetServiceContract => typeof(Ranvier.Dom.StyleSheetService);
    public static System.Type CssQueryServiceContract => typeof(Ranvier.Dom.CssQueryService);
    public static System.Type ComputedStyleServiceContract => typeof(Ranvier.Dom.ComputedStyleService);
    public static System.Type FragmentListContract => typeof(Ranvier.Dom.DOMRectList);
    public static Ranvier.Dom.DOMRectList ElementFragments(Ranvier.Dom.Element element) => element.getClientRects();
    public static Ranvier.Dom.DOMRectList RangeFragments(Ranvier.Dom.Range range) => range.getClientRects();
    public static Ranvier.Dom.DOMRect FirstFragment(Ranvier.Dom.DOMRectList fragments) => fragments.item(0u);
    public static string EscapeIdentifier(string value) => Ranvier.Dom.CSS.escape(value);
    public static Ranvier.Dom.Node Identity(Ranvier.Dom.Node value) => value;
}
