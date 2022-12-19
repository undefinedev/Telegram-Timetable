using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Telegram.Lang;

class Text
{
    public static Dictionary<string, string> Welcome { get; } = new();
    public static Dictionary<string, string> Error { get; } = new();
    public static Dictionary<string, string> Success { get; } = new();
    public static Dictionary<string, string> Account { get; } = new();
    public static Dictionary<string, string> Help { get; } = new();
    public static Dictionary<string, string> History { get; } = new();
    public static Dictionary<string, string> Name { get; } = new();
    public static Dictionary<string, string> Language { get; } = new();
    public static Dictionary<string, string> Specialist { get; } = new();
    public static Dictionary<string, string> FeedbackUser { get; } = new();
    public static Dictionary<string, string> FeedbackSpec { get; } = new();
    public static Dictionary<string, string> HistoryEmpty { get; } = new();
    public static Dictionary<string, string> Back { get; } = new();
    public static Dictionary<string, string> NumberOfOrders { get; } = new();
    public static Dictionary<string, string> NumberSpecOrders { get; } = new();
    public static Dictionary<string, string> NewRecord { get; } = new();
    public static Dictionary<string, string> HelpButton { get; } = new();
    public static Dictionary<string, string> SpecialistsEmpty { get; } = new();
    public static Dictionary<string, string> RecordWarning { get; } = new();
    public static Dictionary<string, string> DateChoose { get; } = new();
    public static Dictionary<string, string> WorkStatus { get; } = new();
    public static Dictionary<string, string> TimeChoose { get; } = new();
    public static Dictionary<string, string> Confirm { get; } = new();
    public static Dictionary<string, string> Decline { get; } = new();
    public static Dictionary<string, string> ConfirmText { get; } = new();
    public static Dictionary<string, string> Rating { get; } = new();
    public static Dictionary<string, string> Holidays { get; } = new();
    public static Dictionary<string, string> Days { get; } = new();
    public static Dictionary<string, string> FutureOrd { get; } = new();
    public static Dictionary<string, string> SpecOrders { get; } = new();
    public static Dictionary<string, string> Completed { get; } = new();
    public static Dictionary<string, string> Rate { get; } = new();
    public static Dictionary<string, string> RateThanks { get; } = new();
    public static Dictionary<string, string> RateNotification { get; } = new();

    public static readonly List<string> SupportedLanguages = new() { "en", "ru" };

    //private static readonly List<string> Phrases = new() { "Welcome", "Error", "Success"};
    private static readonly Dictionary<string, Dictionary<string, string>> Phrases = new()
    {
        { "Welcome", Welcome }, { "Error", Error }, { "Success", Success }, { "Account", Account }, { "Help", Help },
        { "History", History }, { "Name", Name }, { "Language", Language }, { "Spec", Specialist },
        { "FeedbackUser", FeedbackUser }, { "FeedbackSpec", FeedbackSpec }, { "HistoryEmpty", HistoryEmpty },
        { "Back", Back }, { "NumberOfOrders", NumberOfOrders }, { "NumberSpecOrders", NumberSpecOrders },
        { "NewRecord", NewRecord }, { "HelpButton", HelpButton }, { "SpecialistsEmpty", SpecialistsEmpty },
        { "RecordWarning", RecordWarning }, { "DateChoose", DateChoose }, { "WorkStatus", WorkStatus },
        { "TimeChoose", TimeChoose }, { "Confirm", Confirm }, { "Decline", Decline }, { "ConfirmText", ConfirmText }, 
        { "Rating", Rating }, { "Holidays", Holidays }, { "Days", Days }, { "FutureOrd", FutureOrd }, 
        { "SpecOrders", SpecOrders }, { "Completed", Completed }, { "Rate", Rate }, { "RateThanks", RateThanks }, 
        { "RateNotification", RateNotification }
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