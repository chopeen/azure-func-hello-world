#r "Newtonsoft.Json"

using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Primitives;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

public static IActionResult Run(HttpRequest req, TraceWriter log)
{
    // TODO: Is it wrotten to console when executed locally?
    log.Info("C# HTTP trigger function processed a request.");

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

    // TODO: Log some information - notMyArtists, notMyArtistsPlayedRecently, etc

    string result = notMyArtistsPlayedRecently.Count() > 0 ? string.Join(";", notMyArtistsPlayedRecently) : "🎸";
    return new OkObjectResult(result);
}

private static List<string> getRecentArtists(string recentTracksString)
{
    JObject recentTracks = JObject.Parse(recentTracksString);
    // TODO: Can this be improved with a strongly typed model for the Last.fm JSON?
    //       Give "Paste JSON as Code" one more try.
    var tracks = recentTracks["recenttracks"]["track"].ToList();

    return tracks
        .Select(
            x => x["artist"]["#text"].ToString().ToLower().Trim()
        )
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
    // TODO: Log an error when the variable is not defined
    return Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.Process);
}
