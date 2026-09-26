namespace ImprovedCustomizationUI.Customization
{
    public class AppearanceChoice
    {
        public string Head;
        public string Voice;
        public string Upper;
        public string Lower;

        public AppearanceChoice(string head, string voice, string upper, string lower)
        {
            Head = head;
            Voice = voice;
            Upper = upper;
            Lower = lower;
        }

        public bool SameAs(AppearanceChoice other)
        {
            if (other == null)
            {
                return false;
            }

            return Head == other.Head && Voice == other.Voice && Upper == other.Upper && Lower == other.Lower;
        }

        public bool NeedsSaving(AppearanceChoice confirmed)
        {
            if (Head != confirmed.Head || Voice != confirmed.Voice)
            {
                return true;
            }

            if (Upper != "" && Upper != confirmed.Upper)
            {
                return true;
            }

            if (Lower != "" && Lower != confirmed.Lower)
            {
                return true;
            }

            return false;
        }
    }
}
