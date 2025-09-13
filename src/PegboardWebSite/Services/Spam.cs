namespace PegboardWebSite.Services;

public static class Spam
{
    public static bool IsSpamName(string name)
    {
        if (name.Length > 50)
            return true;

        if (name.Any(c => !char.IsLetterOrDigit(c) && c != ' ' && c != '\'' && c != '-' && c != ',' && c != '(' && c != ')' && c != '.') )
        {
            return true;
        }
        return false;
    }
}
