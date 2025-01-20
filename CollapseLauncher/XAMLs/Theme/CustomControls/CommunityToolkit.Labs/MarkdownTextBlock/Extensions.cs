// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable
//using ColorCode;
//using ColorCode.Common;
//using ColorCode.Styling;
using CommunityToolkit.Labs.WinUI.Labs.MarkdownTextBlock.TextElements;
using Markdig.Syntax.Inlines;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Windows.Foundation;
using Windows.UI.ViewManagement;

namespace CommunityToolkit.Labs.WinUI.Labs.MarkdownTextBlock;

public static class Extensions
{
    public const string Blue = "#FF0000FF";
    public const string White = "#FFFFFFFF";
    public const string Black = "#FF000000";
    public const string DullRed = "#FFA31515";
    public const string Yellow = "#FFFFFF00";
    public const string Green = "#FF008000";
    public const string PowderBlue = "#FFB0E0E6";
    public const string Teal = "#FF008080";
    public const string Gray = "#FF808080";
    public const string Navy = "#FF000080";
    public const string OrangeRed = "#FFFF4500";
    public const string Purple = "#FF800080";
    public const string Red = "#FFFF0000";
    public const string MediumTurqoise = "FF48D1CC";
    public const string Magenta = "FFFF00FF";
    public const string OliveDrab = "#FF6B8E23";
    public const string DarkOliveGreen = "#FF556B2F";
    public const string DarkCyan = "#FF008B8B";

    //public static ILanguage ToLanguage(this FencedCodeBlock fencedCodeBlock)
    //{
    //    switch (fencedCodeBlock.Info?.ToLower())
    //    {
    //        case "aspx":
    //            return Languages.Aspx;
    //        case "aspx - vb":
    //            return Languages.AspxVb;
    //        case "asax":
    //            return Languages.Asax;
    //        case "ascx":
    //            return Languages.AspxCs;
    //        case "ashx":
    //        case "asmx":
    //        case "axd":
    //            return Languages.Ashx;
    //        case "cs":
    //        case "csharp":
    //        case "c#":
    //            return Languages.CSharp;
    //        case "xhtml":
    //        case "html":
    //        case "hta":
    //        case "htm":
    //        case "html.hl":
    //        case "inc":
    //        case "xht":
    //            return Languages.Html;
    //        case "java":
    //        case "jav":
    //        case "jsh":
    //            return Languages.Java;
    //        case "js":
    //        case "node":
    //        case "_js":
    //        case "bones":
    //        case "cjs":
    //        case "es":
    //        case "es6":
    //        case "frag":
    //        case "gs":
    //        case "jake":
    //        case "javascript":
    //        case "jsb":
    //        case "jscad":
    //        case "jsfl":
    //        case "jslib":
    //        case "jsm":
    //        case "jspre":
    //        case "jss":
    //        case "jsx":
    //        case "mjs":
    //        case "njs":
    //        case "pac":
    //        case "sjs":
    //        case "ssjs":
    //        case "xsjs":
    //        case "xsjslib":
    //            return Languages.JavaScript;
    //        case "posh":
    //        case "pwsh":
    //        case "ps1":
    //        case "psd1":
    //        case "psm1":
    //            return Languages.PowerShell;
    //        case "sql":
    //        case "cql":
    //        case "ddl":
    //        case "mysql":
    //        case "prc":
    //        case "tab":
    //        case "udf":
    //        case "viw":
    //            return Languages.Sql;
    //        case "vb":
    //        case "vbhtml":
    //        case "visual basic":
    //        case "vbnet":
    //        case "vb .net":
    //        case "vb.net":
    //            return Languages.VbDotNet;
    //        case "rss":
    //        case "xsd":
    //        case "wsdl":
    //        case "xml":
    //        case "adml":
    //        case "admx":
    //        case "ant":
    //        case "axaml":
    //        case "axml":
    //        case "builds":
    //        case "ccproj":
    //        case "ccxml":
    //        case "clixml":
    //        case "cproject":
    //        case "cscfg":
    //        case "csdef":
    //        case "csl":
    //        case "csproj":
    //        case "ct":
    //        case "depproj":
    //        case "dita":
    //        case "ditamap":
    //        case "ditaval":
    //        case "dll.config":
    //        case "dotsettings":
    //        case "filters":
    //        case "fsproj":
    //        case "fxml":
    //        case "glade":
    //        case "gml":
    //        case "gmx":
    //        case "grxml":
    //        case "gst":
    //        case "hzp":
    //        case "iml":
    //        case "ivy":
    //        case "jelly":
    //        case "jsproj":
    //        case "kml":
    //        case "launch":
    //        case "mdpolicy":
    //        case "mjml":
    //        case "mm":
    //        case "mod":
    //        case "mxml":
    //        case "natvis":
    //        case "ncl":
    //        case "ndproj":
    //        case "nproj":
    //        case "nuspec":
    //        case "odd":
    //        case "osm":
    //        case "pkgproj":
    //        case "pluginspec":
    //        case "proj":
    //        case "props":
    //        case "ps1xml":
    //        case "psc1":
    //        case "pt":
    //        case "qhelp":
    //        case "rdf":
    //        case "res":
    //        case "resx":
    //        case "rs":
    //        case "sch":
    //        case "scxml":
    //        case "sfproj":
    //        case "shproj":
    //        case "srdf":
    //        case "storyboard":
    //        case "sublime-snippet":
    //        case "sw":
    //        case "targets":
    //        case "tml":
    //        case "ui":
    //        case "urdf":
    //        case "ux":
    //        case "vbproj":
    //        case "vcxproj":
    //        case "vsixmanifest":
    //        case "vssettings":
    //        case "vstemplate":
    //        case "vxml":
    //        case "wixproj":
    //        case "workflow":
    //        case "wsf":
    //        case "wxi":
    //        case "wxl":
    //        case "wxs":
    //        case "x3d":
    //        case "xacro":
    //        case "xaml":
    //        case "xib":
    //        case "xlf":
    //        case "xliff":
    //        case "xmi":
    //        case "xml.dist":
    //        case "xmp":
    //        case "xproj":
    //        case "xspec":
    //        case "xul":
    //        case "zcml":
    //            return Languages.Xml;
    //        case "php":
    //        case "aw":
    //        case "ctp":
    //        case "fcgi":
    //        case "php3":
    //        case "php4":
    //        case "php5":
    //        case "phps":
    //        case "phpt":
    //            return Languages.Php;
    //        case "css":
    //        case "scss":
    //        case "less":
    //            return Languages.Css;
    //        case "cpp":
    //        case "c++":
    //        case "cc":
    //        case "cp":
    //        case "cxx":
    //        case "h":
    //        case "h++":
    //        case "hh":
    //        case "hpp":
    //        case "hxx":
    //        case "inl":
    //        case "ino":
    //        case "ipp":
    //        case "ixx":
    //        case "re":
    //        case "tcc":
    //        case "tpp":
    //            return Languages.Cpp;
    //        case "ts":
    //        case "tsx":
    //        case "cts":
    //        case "mts":
    //            return Languages.Typescript;
    //        case "fsharp":
    //        case "fs":
    //        case "fsi":
    //        case "fsx":
    //            return Languages.FSharp;
    //        case "koka":
    //            return Languages.Koka;
    //        case "hs":
    //        case "hs-boot":
    //        case "hsc":
    //            return Languages.Haskell;
    //        case "pandoc":
    //        case "md":
    //        case "livemd":
    //        case "markdown":
    //        case "mdown":
    //        case "mdwn":
    //        case "mdx":
    //        case "mkd":
    //        case "mkdn":
    //        case "mkdown":
    //        case "ronn":
    //        case "scd":
    //        case "workbook":
    //            return Languages.Markdown;
    //        case "fortran":
    //        case "f":
    //        case "f77":
    //        case "for":
    //        case "fpp":
    //            return Languages.Fortran;
    //        case "python":
    //        case "py":
    //        case "cgi":
    //        case "gyp":
    //        case "gypi":
    //        case "lmi":
    //        case "py3":
    //        case "pyde":
    //        case "pyi":
    //        case "pyp":
    //        case "pyt":
    //        case "pyw":
    //        case "rpy":
    //        case "smk":
    //        case "spec":
    //        case "tac":
    //        case "wsgi":
    //        case "xpy":
    //            return Languages.Python;
    //        case "matlab":
    //        case "m":
    //            return Languages.MATLAB;
    //        default:
    //            return Languages.JavaScript;
    //    }
    //}

    public static string ToAlphabetical(this int index)
    {
        var alphabetical = "abcdefghijklmnopqrstuvwxyz";
        var remainder = index;
        var stringBuilder = new StringBuilder();
        while (remainder != 0)
        {
            if (remainder > 26)
            {
                var newRemainder = remainder % 26;
                var i = (remainder - newRemainder) / 26;
                stringBuilder.Append(alphabetical[i - 1]);
                remainder = newRemainder;
            }
            else
            {
                stringBuilder.Append(alphabetical[remainder - 1]);
                remainder = 0;
            }
        }
        return stringBuilder.ToString();
    }

    public static TextPointer? GetNextInsertionPosition(this TextPointer position, LogicalDirection logicalDirection)
    {
        // Check if the current position is already an insertion position
        if (position.IsAtInsertionPosition(logicalDirection))
        {
            // Return the same position
            return position;
        }
        else
        {
            // Try to find the next insertion position by moving one symbol forward
            TextPointer next = position.GetPositionAtOffset(1, logicalDirection);
            // If there is no next position, return null
            if (next == null)
            {
                return null;
            }
            else
            {
                // Recursively call this method until an insertion position is found or null is returned
                return GetNextInsertionPosition(next, logicalDirection);
            }
        }
    }

    public static bool IsAtInsertionPosition(this TextPointer position, LogicalDirection logicalDirection)
    {
        // Get the character rect of the current position
        Rect currentRect = position.GetCharacterRect(logicalDirection);
        // Try to get the next position by moving one symbol forward
        TextPointer next = position.GetPositionAtOffset(1, logicalDirection);
        // If there is no next position, return false
        if (next == null)
        {
            return false;
        }
        else
        {
            // Get the character rect of the next position
            Rect nextRect = next.GetCharacterRect(logicalDirection);
            // Compare the two rects and return true if they are different
            return !currentRect.Equals(nextRect);
        }
    }

    public static string RemoveImageSize(string? url)
    {
        if (string.IsNullOrEmpty(url))
        {
            throw new ArgumentException("URL must not be null or empty", nameof(url));
        }

        // Create a regex pattern to match the URL with width and height
        var pattern = @"([^)\s]+)\s*=\s*\d+x\d+\s*";

        // Replace the matched URL with the URL only
        var result = Regex.Replace(url, pattern, "$1", RegexOptions.Compiled);

        return result;
    }

    public static Uri GetUri(string? url, string? @base)
    {
        var validUrl = RemoveImageSize(url);
        Uri result;
#pragma warning disable CS8600 // Converting null literal or possible null value to non-nullable type.
        if (Uri.TryCreate(validUrl, UriKind.Absolute, out result))
        {
            //the url is already absolute
            return result;
        }
        else if (!string.IsNullOrWhiteSpace(@base))
        {
            //the url is relative, so append the base
            //trim any trailing "/" from the base and any leading "/" from the url
            @base = @base.TrimEnd('/');
            validUrl = validUrl.TrimStart('/');
            //return the base and the url separated by a single "/"
            return new Uri(@base + "/" + validUrl);
        }
        else
        {
            //the url is relative to the file system
            //add ms-appx
            validUrl = validUrl.TrimStart('/');
            return new Uri("ms-appx:///" + validUrl);
        }
#pragma warning restore CS8600 // Converting null literal or possible null value to non-nullable type.
    }

    //public static StyleDictionary GetOneDarkProStyle()
    //{
    //    return new StyleDictionary
    //            {
    //                new ColorCode.Styling.Style(ScopeName.PlainText)
    //                {
    //                    Foreground = OneDarkPlainText,
    //                    Background = OneDarkBackground,
    //                    ReferenceName = "plainText"
    //                },
    //                new ColorCode.Styling.Style(ScopeName.HtmlServerSideScript)
    //                {
    //                    Background = Yellow,
    //                    ReferenceName = "htmlServerSideScript"
    //                },
    //                new ColorCode.Styling.Style(ScopeName.HtmlComment)
    //                {
    //                    Foreground = OneDarkComment,
    //                    ReferenceName = "htmlComment"
    //                },
    //                new ColorCode.Styling.Style(ScopeName.HtmlTagDelimiter)
    //                {
    //                    Foreground = OneDarkKeyword,
    //                    ReferenceName = "htmlTagDelimiter"
    //                },
    //                new ColorCode.Styling.Style(ScopeName.HtmlElementName)
    //                {
    //                    Foreground = DullRed,
    //                    ReferenceName = "htmlElementName"
    //                },
    //                new ColorCode.Styling.Style(ScopeName.HtmlAttributeName)
    //                {
    //                    Foreground = Red,
    //                    ReferenceName = "htmlAttributeName"
    //                },
    //                new ColorCode.Styling.Style(ScopeName.HtmlAttributeValue)
    //                {
    //                    Foreground = OneDarkKeyword,
    //                    ReferenceName = "htmlAttributeValue"
    //                },
    //                new ColorCode.Styling.Style(ScopeName.HtmlOperator)
    //                {
    //                    Foreground = OneDarkKeyword,
    //                    ReferenceName = "htmlOperator"
    //                },
    //                new ColorCode.Styling.Style(ScopeName.Comment)
    //                {
    //                    Foreground = OneDarkComment,
    //                    ReferenceName = "comment"
    //                },
    //                new ColorCode.Styling.Style(ScopeName.XmlDocTag)
    //                {
    //                    Foreground = OneDarkXMLComment,
    //                    ReferenceName = "xmlDocTag"
    //                },
    //                new ColorCode.Styling.Style(ScopeName.XmlDocComment)
    //                {
    //                    Foreground = OneDarkXMLComment,
    //                    ReferenceName = "xmlDocComment"
    //                },
    //                new ColorCode.Styling.Style(ScopeName.String)
    //                {
    //                    Foreground = OneDarkString,
    //                    ReferenceName = "string"
    //                },
    //                new ColorCode.Styling.Style(ScopeName.StringCSharpVerbatim)
    //                {
    //                    Foreground = OneDarkString,
    //                    ReferenceName = "stringCSharpVerbatim"
    //                },
    //                new ColorCode.Styling.Style(ScopeName.Keyword)
    //                {
    //                    Foreground = OneDarkKeyword,
    //                    ReferenceName = "keyword"
    //                },
    //                new ColorCode.Styling.Style(ScopeName.PreprocessorKeyword)
    //                {
    //                    Foreground = OneDarkKeyword,
    //                    ReferenceName = "preprocessorKeyword"
    //                },
    //                new ColorCode.Styling.Style(ScopeName.Number)
    //                 {
    //                     Foreground=OneDarkNumber,
    //                     ReferenceName="number"
    //                 },

    //                 new ColorCode.Styling.Style(ScopeName.CssPropertyName)
    //                 {
    //                     Foreground=OneDarkClass,
    //                     ReferenceName="cssPropertyName"
    //                 },

    //                 new ColorCode.Styling.Style(ScopeName.CssPropertyValue)
    //                 {
    //                     Foreground=OneDarkString,
    //                     ReferenceName="cssPropertyValue"
    //                 },

    //                 new ColorCode.Styling.Style(ScopeName.CssSelector)
    //                 {
    //                     Foreground=OneDarkKeyword,
    //                     ReferenceName="cssSelector"
    //                 },

    //                 new ColorCode.Styling.Style(ScopeName.SqlSystemFunction)
    //                 {
    //                     Foreground=OneDarkClass,
    //                     ReferenceName="sqlSystemFunction"
    //                 },

    //                new ColorCode.Styling.Style(ScopeName.XmlAttribute)
    //                {
    //                    Foreground=OneDarkXMLAttribute,
    //                    ReferenceName="xmlAttribute"
    //                },

    //                new ColorCode.Styling.Style(ScopeName.XmlAttributeQuotes)
    //                {
    //                    Foreground=OneDarkXMLDelimiter,
    //                    ReferenceName="xmlAttributeQuotes"
    //                },

    //                new ColorCode.Styling.Style(ScopeName.XmlAttributeValue)
    //                {
    //                    Foreground=OneDarkString,
    //                    ReferenceName="xmlAttributeValue"
    //                },

    //                new ColorCode.Styling.Style(ScopeName.XmlCDataSection)
    //                {
    //                    Foreground=OneDarkXAMLCData,
    //                    ReferenceName="xmlCDataSection"
    //                },

    //                new ColorCode.Styling.Style(ScopeName.XmlComment)
    //                {
    //                    Foreground=OneDarkXMLComment,
    //                    ReferenceName="xmlComment"
    //                },

    //                new ColorCode.Styling.Style(ScopeName.XmlDelimiter)
    //                {
    //                    Foreground=OneDarkXMLDelimiter,
    //                    ReferenceName="xmlDelimiter"
    //                },
    //        new ColorCode.Styling.Style(ScopeName.XmlName)
    //        {
    //            Foreground=OneDarkXMLName,
    //            ReferenceName="xmlName"
    //        }
    //    };
    //}

    public static HtmlElementType TagToType(this string tag)
    {
        return tag.ToLower() switch
               {
                   "address" or "article" or "aside" or "details" or "blockquote" or "canvas" or "dd" or "div" or "dl"
                    or "dt" or "fieldset" or "figcaption" or "figure" or "footer" or "form" or "h1" or "h2" or "h3"
                    or "h4" or "h5" or "h6" or "header" or "hr" or "li" or "main" or "nav" or "noscript" or "ol" or "p"
                    or "pre" or "section" or "table" or "tfoot" or "ul" => HtmlElementType.Block,
                   _ => HtmlElementType.Inline
               };
    }

    public static bool IsHeading(this string tag)
    {
        List<string> headings = ["h1", "h2", "h3", "h4", "h5", "h6"];
        return headings.Contains(tag.ToLower());
    }

    public static Size GetSvgSize(string svgString)
    {
        // Parse the SVG string as an XML document
        XDocument svgDocument = XDocument.Parse(svgString);

        // Get the root element of the document
        XElement? svgElement = svgDocument.Root;

        // Get the height and width attributes of the root element
        XAttribute? heightAttribute = svgElement?.Attribute("height");
        XAttribute? widthAttribute = svgElement?.Attribute("width");

        // Convert the attribute values to double
        double.TryParse(heightAttribute?.Value, NumberStyles.Number, CultureInfo.InvariantCulture, out double height);
        double.TryParse(widthAttribute?.Value, NumberStyles.Number, CultureInfo.InvariantCulture, out double width);

        // Return the height and width as a tuple
        return new Size(width, height);
    }

    public static Size GetMarkdownImageSize(LinkInline link)
    {
        if (link == null || !link.IsImage)
        {
            throw new ArgumentException("Link must be an image", nameof(link));
        }

        var url = link.Url;
        if (string.IsNullOrEmpty(url))
        {
            throw new ArgumentException("Link must have a valid URL", nameof(link));
        }

        // Try to parse the width and height from the URL
        var parts = url.Split('=');
        if (parts.Length == 2)
        {
            var dimensions = parts[1].Split('x');
            if (dimensions.Length == 2 && int.TryParse(dimensions[0], out int width) && int.TryParse(dimensions[1], out int height))
            {
                return new Size(width, height);
            }
        }

        // not using this one as it's seems to be from the HTML renderer
        //// Try to parse the width and height from the special attributes
        //var attributes = link.GetAttributes();
        //if (attributes != null && attributes.Properties != null)
        //{
        //    var width = attributes.Properties.FirstOrDefault(p => p.Key == "width")?.Value;
        //    var height = attributes.Properties.FirstOrDefault(p => p.Key == "height")?.Value;
        //    if (!string.IsNullOrEmpty(width) && !string.IsNullOrEmpty(height) && int.TryParse(width, out int w) && int.TryParse(height, out int h))
        //    {
        //        return new(w, h);
        //    }
        //}

        // Return default values if no width and height are found
        return new Size(0, 0);
    }

    public static SolidColorBrush GetAccentColorBrush()
    {
        // Create a UISettings object to get the accent color
        var uiSettings = new UISettings();

        // Get the accent color as a Color value
        var accentColor = uiSettings.GetColorValue(UIColorType.Accent);

        // Create a SolidColorBrush from the accent color
        var accentBrush = new SolidColorBrush(accentColor);

        return accentBrush;
    }
}
