using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Telegram.Lang;

class Text
{
    public static Dictionary<string, string> Welcome { get; set; } = new();
    public static Dictionary<string, string> Error { get; set; } = new();
    public static Dictionary<string, string> Success { get; set; } = new();
    public static Dictionary<string, string> Account { get; set; } = new();
    public static Dictionary<string, string> Help { get; set; } = new();
    public static Dictionary<string, string> History { get; set; } = new();
    public static Dictionary<string, string> Name { get; set; } = new();
    public static Dictionary<string, string> Language { get; set; } = new();
    public static Dictionary<string, string> Specialist { get; set; } = new();
    public static Dictionary<string, string> FeedbackUser { get; set; } = new();
    public static Dictionary<string, string> FeedbackSpec { get; set; } = new();
    public static Dictionary<string, string> HistoryEmpty { get; set; } = new();
    public static Dictionary<string, string> Back { get; set; } = new();
    public static Dictionary<string, string> NumberOfOrders { get; set; } = new();
    public static Dictionary<string, string> NumberCompletedOrders { get; set; } = new();
    public static Dictionary<string, string> NewRecord { get; set; } = new();
    public static Dictionary<string, string> HelpButton { get; set; } = new();
    public static Dictionary<string, string> SpecialistsEmpty { get; set; } = new();
    
    public static readonly List<string> SupportedLanguages = new() { "en", "ru" };

    //private static readonly List<string> Phrases = new() { "Welcome", "Error", "Success"};
    private static readonly Dictionary<string, Dictionary<string, string>> Phrases = new()
    {
        { "Welcome", Welcome }, { "Error", Error }, { "Success", Success }, { "Account", Account }, { "Help", Help },
        { "History", History }, { "Name", Name }, { "Language", Language }, { "Spec", Specialist },
        { "FeedbackUser", FeedbackUser }, { "FeedbackSpec", FeedbackSpec }, { "HistoryEmpty", HistoryEmpty },
        { "Back", Back }, { "NumberOfOrders", NumberOfOrders }, { "NumberCompletedOrders", NumberCompletedOrders },
        { "NewRecord", NewRecord }, { "HelpButton", HelpButton }, { "SpecialistsEmpty", SpecialistsEmpty }
    };


    public Text()
    {
        using var reader = new StreamReader($"{Environment.CurrentDirectory}/Resources/text.json");
        var json = JToken.ReadFrom(new JsonTextReader(reader));


        foreach (var phrase in Phrases)
        {
            foreach (var wel in json[phrase.Key]!)
            {
                var welProp = wel.ToObject<JProperty>();
                phrase.Value.Add(welProp!.Name, welProp.Value.ToString());
            }
        }
    }
}