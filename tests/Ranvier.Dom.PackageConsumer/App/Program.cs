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
Console.WriteLine("DOM_PACKAGE_IDENTITY PASS " + Bridge.NodeContract.Assembly.FullName);
