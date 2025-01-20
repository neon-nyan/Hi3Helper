using Microsoft.UI.Xaml.Media;

namespace CollapseLauncher.Extension
{
    internal static class FontCollections
    {
        internal static FontFamily FontAwesomeSolid
        {
            get
            {
                if (field == null)
                    field = UIElementExtensions.GetApplicationResource<FontFamily>("FontAwesomeSolid");

                return field;
            }
        }

        internal static FontFamily FontAwesomeRegular
        {
            get
            {
                if (field == null)
                    field = UIElementExtensions.GetApplicationResource<FontFamily>("FontAwesome");

                return field;
            }
        }

        internal static FontFamily FontAwesomeBrand
        {
            get
            {
                if (field == null)
                    field = UIElementExtensions.GetApplicationResource<FontFamily>("FontAwesomeBrand");

                return field;
            }
        }
    }
}
