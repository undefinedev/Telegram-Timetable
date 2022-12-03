using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Telegram.Lang;

class Text
{
    public static Dictionary<string, string> Welcome { get; set; } = new();
    public static Dictionary<string, string> Error { get; set; } = new();
    public static Dictionary<string, string> Success { get; set; } = new();
    public static readonly List<string> SupportedLanguages = new() { "English", "Russian" };
    private static readonly List<string> Phrases = new() { "Welcome", "Error", "Success"};

    public Text()
    {
        using var reader = new StreamReader($"{Environment.CurrentDirectory}/Resources/text.json");
        var json = JToken.ReadFrom(new JsonTextReader(reader));


        foreach (var phrase in Phrases)
        {
            
            foreach (var wel in json[phrase]!)
            {
                var welProp = wel.ToObject<JProperty>();
                switch (phrase)
                {
                    case "Welcome" : 
                        Welcome.Add(welProp!.Name, welProp.Value.ToString());
                        break;
                    case "Error" :
                        Error.Add(welProp!.Name, welProp.Value.ToString());
                        break;
                    case "Success" :
                        Success.Add(welProp!.Name, welProp.Value.ToString());
                        break;
                }
            }
        }
    }
}