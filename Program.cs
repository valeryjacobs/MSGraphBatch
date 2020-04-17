﻿using Microsoft.Extensions.Configuration;
using Microsoft.Graph;
using Microsoft.Identity.Client;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace MSGraphBatch
{
    class Program
    {

        static async Task Main(string[] args)
        {
            var builder = new ConfigurationBuilder()
               .AddEnvironmentVariables()
               .SetBasePath(System.IO.Directory.GetCurrentDirectory())
               .AddJsonFile("local.settings.json");

            var config = builder.Build();

            Console.WriteLine("Getting token..");

            var accessToken = await GetTokenAsync(config);

            GraphServiceClient graphClient = new GraphServiceClient(new DelegateAuthenticationProvider((requestMessage) =>
            {
                requestMessage
                    .Headers
                    .Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

                return Task.FromResult(0);
            }));

            await AddEventsInBatch(config, graphClient);

            Console.ReadLine();
        }

        private static async Task AddEventsInBatch(IConfigurationRoot config, GraphServiceClient client)
        {
            var events = new List<Event>();


            for (int i = 0; i < 40; i++)
            {
                var @event = new Event
                {
                    Subject = "Subject" + i,
                    Body = new ItemBody
                    {
                        ContentType = BodyType.Html,
                        Content = "Content" + i
                    },
                    Start = new DateTimeTimeZone
                    {
                        DateTime = DateTime.UtcNow.AddHours(i).ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture),
                        TimeZone = TimeZoneInfo.Utc.Id
                    },
                    End = new DateTimeTimeZone
                    {
                        DateTime = DateTime.UtcNow.AddHours(i).AddMinutes(30).ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture),
                        TimeZone = TimeZoneInfo.Utc.Id
                    },
                    Location = new Location
                    {
                        DisplayName = "Dummy location"
                    }
                };

                events.Add(@event);
            }

            List<BatchRequestContent> batches = new List<BatchRequestContent>();

            var batchRequestContent = new BatchRequestContent();

            foreach (Event e in events)
            {
                var httpRequestMessage = new HttpRequestMessage(HttpMethod.Post, $"https://graph.microsoft.com/v1.0/users/{config["CalendarEmail"]}/events")
                {
                    Content = new StringContent(JsonConvert.SerializeObject(e), Encoding.UTF8, "application/json")
                };

                BatchRequestStep requestStep = new BatchRequestStep(events.IndexOf(e).ToString(), httpRequestMessage, null);
                batchRequestContent.AddBatchRequestStep(requestStep);

                // Max number of 20 request per batch. So we need to send out multiple batches.
                if (events.IndexOf(e) > 0 && events.IndexOf(e) % 20 == 0)
                {
                    batches.Add(batchRequestContent);
                    batchRequestContent = new BatchRequestContent();
                }
            }

            if (batches.Count == 0 && batchRequestContent != null) batches.Add(batchRequestContent);

            Console.WriteLine("Creating events in batches...");

            List<string> createdEvents = new List<string>();

            foreach (BatchRequestContent batch in batches)
            {
                BatchResponseContent response = null;

                try
                {
                    response = await client.Batch.Request().PostAsync(batch);
                }
                catch (Microsoft.Graph.ClientException ex)
                {
                    Console.WriteLine(ex.Message);
                }

                Dictionary<string, HttpResponseMessage> responses = await response.GetResponsesAsync();


                foreach (string key in responses.Keys)
                {
                    HttpResponseMessage httpResponse = await response.GetResponseByIdAsync(key);
                    var responseContent = await httpResponse.Content.ReadAsStringAsync();

                    JObject rss = JObject.Parse(responseContent);

                    createdEvents.Add((string)rss["id"]);

                    string nextLink = await response.GetNextLinkAsync();
                    Console.WriteLine($"Response code: {responses[key].StatusCode}-{responses[key].Content.ReadAsStringAsync().Result.Substring(0, 255)}");
                }
            }

            Console.WriteLine("Events created. Press enter to remove them from the calendar.");
            Console.ReadLine();
            Console.WriteLine($"Removing {createdEvents.Count} events...");

            foreach (string eventId in createdEvents)
            {
                await client.Users[config["CalendarEmail"]].Events[eventId]
                  .Request()
                  .DeleteAsync();

            }

            Console.WriteLine($"{createdEvents.Count} events where removed from calendar.");
            Console.ReadLine();
        }

        private static async Task<string> GetTokenAsync(IConfigurationRoot config)
        {
            var clientSecret = config["ClientSecret"];
            var tenantId = config["TenantId"];
            var clientId = config["ClientId"];
            var authority = config["Authority"];
            var scope = config["Scope"];

            var app = ConfidentialClientApplicationBuilder.Create(clientId)
                .WithClientSecret(clientSecret)
                .WithAuthority(new Uri(authority + tenantId))
                .Build();

            string[] scopes = new string[] { scope };

            AuthenticationResult acquireTokenResult = await app.AcquireTokenForClient(scopes).ExecuteAsync();


            return acquireTokenResult.AccessToken;
        }
    }
}
