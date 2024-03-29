﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace GraphTutorial
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine(".NET Core Graph Tutorial\n");

            var appConfig = LoadAppSettings();

            if (appConfig == null)
            {
                Console.WriteLine("Missing or invalid appsettings.json...exiting");
                return;
            }

            var appId = appConfig["appId"];
            var scopesString = appConfig["scopes"];
            var scopes = scopesString.Split(';');

            // Initialize Graph client
            GraphHelper.Initialize(appId, scopes, (code, cancellation) => {
                Console.WriteLine(code.Message);
                return Task.FromResult(0);
            });

            var accessToken = GraphHelper.GetAccessTokenAsync(scopes).Result;

            // Get signed in user
            var user = GraphHelper.GetMeAsync().Result;
            Console.WriteLine($"Welcome {user.DisplayName}!\n");

            // Check for timezone and date/time formats in mailbox settings
            // Use defaults if absent
            var userTimeZone = !string.IsNullOrEmpty(user.MailboxSettings?.TimeZone) ?
                user.MailboxSettings?.TimeZone : TimeZoneInfo.Local.StandardName;
            var userDateFormat = !string.IsNullOrEmpty(user.MailboxSettings?.DateFormat) ?
                user.MailboxSettings?.DateFormat : CultureInfo.CurrentCulture.DateTimeFormat.ShortDatePattern;
            var userTimeFormat = !string.IsNullOrEmpty(user.MailboxSettings?.TimeFormat) ?
                user.MailboxSettings?.TimeFormat : CultureInfo.CurrentCulture.DateTimeFormat.ShortTimePattern;

            int choice = -1;

            while (choice != 0) {
                Console.WriteLine("Please choose one of the following options:");
                Console.WriteLine("0. Exit");
                Console.WriteLine("1. Display access token");
                Console.WriteLine("2. View this week's calendar");
                Console.WriteLine("3. Add an event");

                try
                {
                    choice = int.Parse(Console.ReadLine());
                }
                catch (System.FormatException)
                {
                    // Set to invalid value
                    choice = -1;
                }

                switch(choice)
                {
                    case 0:
                        // Exit the program
                        Console.WriteLine("Goodbye...");
                        break;

                    case 1:
                        // Display access token
                        Console.WriteLine($"Access token: {accessToken}\n");
                        break;

                    case 2:
                        // List the calendar
                        ListCalendarEvents(
                            userTimeZone,
                            $"{userDateFormat} {userTimeFormat}"
                        );
                        break;

                    case 3:
                        // Create a new event
                        CreateEvent(userTimeZone);
                        break;

                    default:
                        Console.WriteLine("Invalid choice! Please try again.");
                        break;
                }
            }
        }

        static void ListCalendarEvents(string userTimeZone, string dateTimeFormat)
        {
            var events = GraphHelper
                .GetCurrentWeekCalendarViewAsync(DateTime.Today, userTimeZone)
                .Result;

            Console.WriteLine("Events:");

            foreach (var calendarEvent in events)
            {
                Console.WriteLine($"Subject: {calendarEvent.Subject}");
                Console.WriteLine($"  Organizer: {calendarEvent.Organizer.EmailAddress.Name}");
                Console.WriteLine($"  Start: {FormatDateTimeTimeZone(calendarEvent.Start, dateTimeFormat)}");
                Console.WriteLine($"  End: {FormatDateTimeTimeZone(calendarEvent.End, dateTimeFormat)}");
            }
        }

        static string FormatDateTimeTimeZone(
            Microsoft.Graph.DateTimeTimeZone value,
            string dateTimeFormat)
        {
            // Parse the date/time string from Graph into a DateTime
            var dateTime = DateTime.Parse(value.DateTime);

            return dateTime.ToString(dateTimeFormat);
        }

        static void CreateEvent(string userTimeZone)
        {
            // Prompt user for info

            // Require a subject
            var subject = GetUserInput("subject", true,
                (input) => {
                    return GetUserYesNo($"Subject: {input} - is that right?");
                });

            // Attendees are optional
            var attendeeList = new List<string>();
            if (GetUserYesNo("Do you want to invite attendees?"))
            {
                string attendee = null;

                do
                {
                    attendee = GetUserInput("attendee", false,
                        (input) => {
                            return GetUserYesNo($"{input} - add attendee?");
                        });

                    if (!string.IsNullOrEmpty(attendee))
                    {
                        attendeeList.Add(attendee);
                    }
                }
                while (!string.IsNullOrEmpty(attendee));
            }

            var startString = GetUserInput("event start", true,
                (input) => {
                    // Validate that input is a date
                    return (DateTime.TryParse(input, out var result));
                });

            var start = DateTime.Parse(startString);

            var endString = GetUserInput("event end", true,
                (input) => {
                    // Validate that input is a date
                    // and is later than start
                    return (DateTime.TryParse(input, out var result) &&
                        result.CompareTo(start) > 0);
                });

            var end = DateTime.Parse(endString);

            var body = GetUserInput("body", false,
                (input => { return true; }));

            Console.WriteLine($"Subject: {subject}");
            Console.WriteLine($"Attendees: {string.Join(";", attendeeList)}");
            Console.WriteLine($"Start: {start.ToString()}");
            Console.WriteLine($"End: {end.ToString()}");
            Console.WriteLine($"Body: {body}");
            if (GetUserYesNo("Create event?"))
            {
                GraphHelper.CreateEvent(
                    userTimeZone,
                    subject,
                    start,
                    end,
                    attendeeList,
                    body).Wait();
            }
            else
            {
                Console.WriteLine("Canceled.");
            }
        }

        static bool GetUserYesNo(string prompt)
        {
            Console.Write($"{prompt} (y/n)");
            ConsoleKeyInfo confirm;
            do
            {
                confirm = Console.ReadKey(true);
            }
            while (confirm.Key != ConsoleKey.Y && confirm.Key != ConsoleKey.N);

            Console.WriteLine();
            return (confirm.Key == ConsoleKey.Y);
        }

        static string GetUserInput(
            string fieldName,
            bool isRequired,
            Func<string, bool> validate)
        {
            string returnValue = null;
            do
            {
                Console.Write($"Enter a {fieldName}: ");
                if (!isRequired)
                {
                    Console.Write("(ENTER to skip) ");
                }
                var input = Console.ReadLine();

                if (!string.IsNullOrEmpty(input))
                {
                    if (validate.Invoke(input))
                    {
                        returnValue = input;
                    }
                }
            }
            while (string.IsNullOrEmpty(returnValue) && isRequired);

            return returnValue;
        }

        static IConfigurationRoot LoadAppSettings()
        {
            var appConfig = new ConfigurationBuilder()
                .AddUserSecrets<Program>()
                .Build();

            // Check for required settings
            if (string.IsNullOrEmpty(appConfig["appId"]) ||
                string.IsNullOrEmpty(appConfig["scopes"]))
            {
                return null;
            }

            return appConfig;
        }
    }
}