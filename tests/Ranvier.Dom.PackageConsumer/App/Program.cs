using System;

if (Bridge.NodeContract != typeof(Ranvier.Dom.Node))
    throw new Exception("Browser and native packages have different public contract identities");
if (Bridge.Identity((Ranvier.Dom.Node)null) != null)
    throw new Exception("A cross-package interface call changed the value");
if (!typeof(Ranvier.Dom.CSSStyleDeclaration).IsAssignableFrom(typeof(Ranvier.Dom.CSSStyleProperties)))
    throw new Exception("Inline style properties lost the CSS declaration contract");
if (typeof(Ranvier.Dom.ElementCSSInlineStyle).GetProperty("style")?.PropertyType != typeof(Ranvier.Dom.CSSStyleProperties))
    throw new Exception("The inline style getter is missing from the public mixin");
if (typeof(Ranvier.Dom.Attr).GetProperty("style")?.PropertyType != typeof(Ranvier.Dom.CSSStyleProperties))
    throw new Exception("The attribute style getter is missing from the public contract");
if (Bridge.TableContract != typeof(Ranvier.Dom.HTMLTableElement))
    throw new Exception("Browser and native packages have different table contract identities");
if (typeof(Ranvier.Dom.HTMLTableElement).GetMethod("insertRow")?.ReturnType != typeof(Ranvier.Dom.HTMLTableRowElement))
    throw new Exception("The typed table row insertion contract is missing");
if (typeof(Ranvier.Dom.HTMLTableElement).GetProperty("rows")?.PropertyType != typeof(Ranvier.Dom.HTMLCollection))
    throw new Exception("The live table rows collection contract is missing");
if (typeof(Ranvier.Dom.HTMLTableSectionElement).GetMethod("deleteRow") == null)
    throw new Exception("The table section row deletion contract is missing");
if (typeof(Ranvier.Dom.HTMLTableRowElement).GetMethod("insertCell") == null)
    throw new Exception("The table cell insertion contract is missing");
if (typeof(Ranvier.Dom.HTMLTableCellElement).GetProperty("colSpan")?.PropertyType != typeof(uint))
    throw new Exception("The reflected table cell span contract is missing");
if (typeof(Ranvier.Dom.HTMLTableColElement).GetProperty("span")?.PropertyType != typeof(uint))
    throw new Exception("The reflected table column span contract is missing");
if (typeof(Ranvier.Dom.HTMLTimeElement).GetProperty("dateTime")?.PropertyType != typeof(string))
    throw new Exception("The typed time reflection contract is missing");
if (typeof(Ranvier.Dom.HTMLDataListElement).GetProperty("options")?.PropertyType != typeof(Ranvier.Dom.HTMLCollection))
    throw new Exception("The live datalist options contract is missing");
if (typeof(Ranvier.Dom.HTMLSourceElement).GetProperty("width")?.PropertyType != typeof(uint))
    throw new Exception("The reflected source dimensions contract is missing");
if (typeof(Ranvier.Dom.HTMLModElement).GetProperty("cite")?.PropertyType != typeof(string))
    throw new Exception("The edit annotation reflection contract is missing");
if (!typeof(Ranvier.Dom.ElementCSSInlineStyle).IsAssignableFrom(typeof(Ranvier.Dom.MathMLElement)))
    throw new Exception("MathML elements lost the shared inline style contract");
if (!typeof(Ranvier.Dom.HTMLOrSVGElement).IsAssignableFrom(typeof(Ranvier.Dom.MathMLElement)))
    throw new Exception("MathML elements lost the shared focus/data contract");
if (typeof(Ranvier.Dom.SVGElement).GetProperty("viewportElement")?.PropertyType != typeof(Ranvier.Dom.SVGElement))
    throw new Exception("The SVG element introspection contract is missing");
if (typeof(Ranvier.Dom.CSSStyleSheet).GetMethod("insertRule")?.ReturnType != typeof(uint))

    throw new Exception("The stylesheet rule insertion contract is missing");

if (typeof(Ranvier.Dom.CSSStyleSheet).GetProperty("cssRules")?.PropertyType.FullName != "Ranvier.Dom.CSSRuleList")

    throw new Exception("The stylesheet rule collection contract is missing");

if (typeof(Ranvier.Dom.HTMLAreaElement).GetProperty("coords")?.PropertyType != typeof(string))

    throw new Exception("The area reflection contract is missing");

var descriptors = typeof(Ranvier.Dom.Node).Assembly.GetType("Ranvier.Dom.CSSFontFaceDescriptors");

if (descriptors?.GetProperty("font-family")?.PropertyType != typeof(string))

    throw new Exception("The dashed CSS descriptor alias contract is missing");

Console.WriteLine("DOM_PACKAGE_IDENTITY PASS " + Bridge.NodeContract.Assembly.FullName);
