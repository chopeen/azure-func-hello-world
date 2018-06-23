#r "Newtonsoft.Json"
#r "SendGrid"


using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Primitives;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SendGrid;
using SendGrid.Helpers.Mail;

public static void Run(TimerInfo timer, TraceWriter log, out SendGridMessage message)
{
    // when executed locally, logged to the console
    log.Info("Request processing started.");

    string recentTracksString = "";
    using (HttpClient client = new HttpClient())
    {
        // using GetStringAsync in a way that makes a synchronous call; it simplifies the code,
        //   but let's see how soon it becomes a problem
        recentTracksString = client.GetStringAsync(getRecentTracksUri()).Result;
    }

    var recentArtists = getRecentArtists(recentTracksString);
    var notMyArtists = getNotMyArtists();
    var notMyArtistsPlayedRecently = recentArtists.Intersect(notMyArtists);

    // debugging helper
    // notMyArtistsPlayedRecently.ToList().ForEach(x => Console.WriteLine(x));

    log.Info(
        string.Format("Recent artists:       {0} [{1}]", string.Join("; ", recentArtists), recentArtists.Count())
    );
    log.Info(
        string.Format("Blacklisted artists:  {0} [{1}]", string.Join("; ", notMyArtists), notMyArtists.Count())
    );

    bool needSendAlert = (notMyArtistsPlayedRecently.Count() > 0);

    log.Info(
        string.Format("Request processing finished - {0}alert sent.", needSendAlert ? "" : "no ")
    );

    // TODO: This is to test that email sending actually works - start sending actual alerts:
    //       1. Only send an email when needed
    //       2. Include a list of artists (ideally with track names) to investigate

    message = getAlertMessage();
}

private static SendGridMessage getAlertMessage()
{
    // docs: https://github.com/sendgrid/sendgrid-csharp/blob/master/src/SendGrid/Helpers/Mail/MailHelper.cs#L137
    EmailAddress from = MailHelper.StringToEmailAddress(getLocalSetting("EmailFromAlert"));
    EmailAddress to = MailHelper.StringToEmailAddress(getLocalSetting("EmailToAlert"));
    
    string subject = "Hello!";
    // TODO: Plain content should be created automatically by stripping tags from HTML context
    string plainTextContent = "This is the message body. Hope you're ok.";
    string htmlContent = "This is the message body. <strong>Hope you're ok</strong>.";

    // docs: https://github.com/sendgrid/sendgrid-csharp/blob/master/src/SendGrid/Helpers/Mail/MailHelper.cs#L31
    var message = MailHelper.CreateSingleEmail(from, to, subject, plainTextContent, htmlContent);  

    return message;
}

private static List<string> getRecentArtists(string recentTracksString)
{
    JObject recentTracks = JObject.Parse(recentTracksString);
    // TODO: Can this be improved with a strongly typed model for the Last.fm JSON?
    //       Give "Paste JSON as Code" one more try (https://github.com/quicktype/quicktype-vscode/issues/8).
    var tracks = recentTracks["recenttracks"]["track"].ToList();

    return tracks
        .Select(
            x => x["artist"]["#text"].ToString().ToLower().Trim()
        )
        .Distinct()
        .ToList();
}

private static List<string> getNotMyArtists()
{
    return getLocalSetting("NotMyArtists")
        .Split(';')
        .ToList()
        .Select(
            x => x.ToLower().Trim()
            )
        .ToList();
}

private static string getRecentTracksUri()
{
    // TODO: Is is enough to fetch only 1 page of results?
    return string.Format(
        "http://ws.audioscrobbler.com/2.0/?method={0}&user={1}&api_key={2}&format=json",
        "user.getrecenttracks",
        getLocalSetting("LastFmUser"),
        getLocalSetting("LastFmKey")
    );
}

private static string getLocalSetting(string name)
{
    string value = Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.Process);
    if (string.IsNullOrEmpty(value))
    {
        throw new NullReferenceException(
            string.Format("Environment variable {0} has no value or is not defined.", name)
        );
    }
    return value;
}
