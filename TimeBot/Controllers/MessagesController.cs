using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Http;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Connector;
using System;
using Microsoft.Bot.Builder.Luis;
using Microsoft.Bot.Builder.Luis.Models;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Configuration;
using System.Data;
using System.IO;

namespace TimeBot
{
    [BotAuthentication]
    public class MessagesController : ApiController
    {
        /// <summary>
        /// POST: api/Messages
        /// Receive a message from a user and reply to it
        /// </summary>
        /// 
        public async Task<HttpResponseMessage> Post([FromBody]Activity activity)
        {

            
            if (activity.Type == ActivityTypes.Message)
            {
                //

                //Log to DB
                //Instantiate the BotData dbContext
                Models.TimeBotDBEntities1 db = new Models.TimeBotDBEntities1();
                //Create a new User object
                Models.User newUser = new Models.User();
                //Set properties of user object
                newUser.UserID = activity.From.Id;
                newUser.UserName = activity.From.Name;

                StateClient stateClient = activity.GetStateClient();
                BotData botData = stateClient.BotState.GetPrivateConversationData(activity.ChannelId, activity.Conversation.Id, activity.From.Id);

                if (!isExistingUser(newUser))
                {
                    newUser.ExistingUser = 0;

                    db.Users.Add(newUser);
                    db.SaveChangesAsync();
                        
                    botData.SetProperty<bool>("isExistingUser", false);

                }   
                else
                {
                    botData.SetProperty<bool>("isExistingUser", true);
                    botData.SetProperty<string>("Username", newUser.UserName);
                    botData.SetProperty<int>("Id", Int32.Parse(activity.From.Id));
                }

                await Conversation.SendAsync(activity, () => new GetTimeDialog());

            }
            else
            {
                HandleSystemMessage(activity);
            }
            var response = Request.CreateResponse(HttpStatusCode.OK);
            return response;            
        }
        private bool isExistingUser(Models.User user)
        {
            Models.TimeBotDBEntities1 db = new Models.TimeBotDBEntities1();

            if (db.Users.FindAsync(user.UserID) == null)
            {
                return true;
            }
            return false;
        }

        private Activity HandleSystemMessage(Activity message)
        {
            if (message.Type == ActivityTypes.DeleteUserData)
            {
                // Implement user deletion here
                // If we handle user deletion, return a real message
            }
            else if (message.Type == ActivityTypes.ConversationUpdate)
            {
                // Handle conversation state changes, like members being added and removed
                // Use Activity.MembersAdded and Activity.MembersRemoved and Activity.Action for info
                // Not available in all channels
            }
            else if (message.Type == ActivityTypes.ContactRelationUpdate)
            {
                // Handle add/remove from contact lists
                // Activity.From + Activity.Action represent what happened
            }
            else if (message.Type == ActivityTypes.Typing)
            {
                // Handle knowing tha the user is typing
            }
            else if (message.Type == ActivityTypes.Ping)
            {
            }

            return null;
        }
    }
    [LuisModel("fa643f4b-d76d-4b61-bd2e-cf85d520b7b8", "137fcf0303804aa5a0a727186a577d8d", domain: "westus.api.cognitive.microsoft.com")]
    [Serializable]
    public class GetTimeDialog: LuisDialog<object>
    {
        private readonly Dictionary<string, Event> eventByTitle = new Dictionary<string, Event>();
        //Careful - "event" is a reserved word
        [Serializable]
        public sealed class Event : IEquatable<Event>
        {
            public string Name { get; set; }
            public string Time { get; set; }
            public string PersonName { get; set; }
            public override string ToString()
            {
                return $"[{this.Name} is at {this.Time}]";
            }

            public bool Equals(Event other)
            {
                return other != null && this.Name == other.Name && this.Time == other.Time && this.PersonName == other.PersonName;
            }

            public override bool Equals(object other)
            {
                return Equals(other as Event);
            }
            public override int GetHashCode()
            {
                return this.Name.GetHashCode();
            }
        }

        //CONSTANTS
        public const string Entity_Event_Name = "Event.Name";
        public const string Entity_Event_DateTime = "builtin.datetimeV2.time";
        public const string DefaultEventName = "default";
        public const string Entity_Event_Person_Name = "Event.PersonName";


        //Greeting
        [LuisIntent("Greeting")]
        public async Task GreetingIntent(IDialogContext context, LuisResult result)
        {
            if (result.TopScoringIntent.Score < 0.6)
            {
                None(context);
            }
            else
            {
                Models.User user = GetUser(context);
                //bool existingUser;
                //context.PrivateConversationData.TryGetValue<bool>("isExistingUser", out existingUser);
                if (user.ExistingUser.Equals(1))
                {
                    context.PrivateConversationData.SetValue<string>("Username", user.UserName);

                    await context.PostAsync($"Hi {user.UserName}, welcome back!");
                }
                else
                {
                    PromptDialog.Text(context, After_UsernamePrompt, "Hi there! I don't this we've met, what shall I call you?");
                }
            }

        }

        private async Task After_UsernamePrompt(IDialogContext context, IAwaitable<string> result)
        {
            string username = await result;

            Models.TimeBotDBEntities1 db = new Models.TimeBotDBEntities1();
            Models.User newUser = new Models.User();
            int id;
            context.PrivateConversationData.TryGetValue<int>("Id", out id);

            newUser = db.Users.FindAsync(id).Result;
            newUser.UserName = username;
            newUser.ExistingUser = 1;
            db.SaveChangesAsync();

            context.PrivateConversationData.SetValue<string>("Username", username);
            await context.PostAsync($"Pleasure to meet you {username}.");
            await context.PostAsync("What can I do for you?");
            context.Wait(MessageReceived);
        }

        private Models.User GetUser(IDialogContext context)
        {
            Models.TimeBotDBEntities1 db = new Models.TimeBotDBEntities1();
            Models.User user = new Models.User();
            int id;
            context.PrivateConversationData.TryGetValue<int>("Id", out id);

            user = db.Users.FindAsync(id).Result;
            return user;
        }

        //Farewell
        [LuisIntent("Farewell")]
        public async Task FarewellIntent(IDialogContext context, LuisResult result)
        {
            if (result.TopScoringIntent.Score < 0.6)
            {
                None(context);
            }
            else
            {

                string username;
                if (context.PrivateConversationData.TryGetValue<string>("Username", out username))
                {
                    await context.PostAsync($"Until we meet again {username}, take care.");
                }
                else
                {
                    await context.PostAsync("Until we meet again stranger, take care.");
                }
            }
            
        }

        //Get the time
        [LuisIntent("Time.GetTime")]
        public async Task GetTimeIntent(IDialogContext context, LuisResult result)
        {
            if (result.TopScoringIntent.Score < 0.6)
            {
                None(context);
            }
            else
            {

                TimeRetriver timeRetriver = new TimeRetriver();
                await context.PostAsync("The current time is: " + timeRetriver.ToString());
                context.Wait(MessageReceived);
            }
        }

        private Event eventToCreate;
        private string currentName;
        private bool existingTime;
        private bool existingName;
        private bool existingPerson;
        [LuisIntent("Event.Create")]
        public Task CreateEventIntent(IDialogContext context, LuisResult result)
        {

            if (result.TopScoringIntent.Score < 0.6)
            {
                None(context);
                return Task.CompletedTask;
            }
            else
            {

                //Find the name of the event
                EntityRecommendation name;
                EntityRecommendation time;
                EntityRecommendation person;
                existingTime = false;
                existingName = false;
                existingPerson = false;
                if ((!result.TryFindEntity(Entity_Event_Name, out name)) && (!result.TryFindEntity(Entity_Event_DateTime, out time)) && (!result.TryFindEntity(Entity_Event_Person_Name, out person)))
                {
                    name = new EntityRecommendation(type: Entity_Event_Name) { Entity = DefaultEventName };
                    //Create the new event object
                    var newEvent = new Event() { Name = name.Entity};
                    //Add the new event to the list of events and also save it in order to add content to it later
                    eventToCreate = this.eventByTitle[newEvent.Name] = newEvent;
                    PromptDialog.Text(context, After_NamePrompt, "What would you like to call your event?");
                }
                else if ((!result.TryFindEntity(Entity_Event_Name, out name)) && (!result.TryFindEntity(Entity_Event_DateTime, out time)) && (result.TryFindEntity(Entity_Event_Person_Name, out person)))
                {
                    name = new EntityRecommendation(type: Entity_Event_Name) { Entity = DefaultEventName };
                    //Create the new event object
                    var newEvent = new Event() { Name = name.Entity, PersonName = person.Entity };
                    //Add the new event to the list of events and also save it in order to add content to it later
                    eventToCreate = this.eventByTitle[newEvent.Name] = newEvent;
                    eventToCreate.PersonName = newEvent.PersonName;
                    existingPerson = true;
                    PromptDialog.Text(context, After_NamePrompt, "What would you like to call this event?");
                }                      
                else if ((result.TryFindEntity(Entity_Event_Name, out name)) && (!result.TryFindEntity(Entity_Event_DateTime, out time)) && (!result.TryFindEntity(Entity_Event_Person_Name, out person)))
                {
                    //Create the new event object
                    var newEvent = new Event() { Name = name.Entity };
                    //Add the new event to the list of events and also save it in order to add content to it later
                    eventToCreate = this.eventByTitle[newEvent.Name] = newEvent;
                    existingName = true;
                    //Ask the user what time they want the event to happen
                    PromptDialog.Text(context, After_TimePrompt, "What time would you like to schedule this event?");
                }
                else if ((result.TryFindEntity(Entity_Event_Name, out name)) && (!result.TryFindEntity(Entity_Event_DateTime, out time)) && (result.TryFindEntity(Entity_Event_Person_Name, out person)))
                {
                    //Create the new event object
                    var newEvent = new Event() { Name = name.Entity, PersonName = person.Entity };
                    //Add the new event to the list of events and also save it in order to add content to it later
                    eventToCreate = this.eventByTitle[newEvent.Name] = newEvent;
                    existingName = true;
                    //Ask the user what time they want the event to happen
                    PromptDialog.Text(context, After_TimePrompt, "What time would you like to schedule this event?");
                }
                else if ((!result.TryFindEntity(Entity_Event_Name, out name)) && (result.TryFindEntity(Entity_Event_DateTime, out time)) && (!result.TryFindEntity(Entity_Event_Person_Name, out person)))
                {
                    name = new EntityRecommendation(type: Entity_Event_Name) { Entity = DefaultEventName };
                    //Create the new event object
                    var newEvent = new Event() { Name = name.Entity, Time = time.Entity };
                    //Add the new event to the list of events and also save it in order to add content to it later
                    eventToCreate = this.eventByTitle[newEvent.Name] = newEvent;
                    existingTime = true;
                    PromptDialog.Text(context, After_NamePrompt, "What would you like to call this event?");
                }
                else if ((!result.TryFindEntity(Entity_Event_Name, out name)) && (result.TryFindEntity(Entity_Event_DateTime, out time)) && (result.TryFindEntity(Entity_Event_Person_Name, out person)))
                {
                    name = new EntityRecommendation(type: Entity_Event_Name) { Entity = DefaultEventName };
                    //Create the new event object
                    var newEvent = new Event() { Name = name.Entity, Time = time.Entity , PersonName = person.Entity};
                    //Add the new event to the list of events and also save it in order to add content to it later
                    eventToCreate = this.eventByTitle[newEvent.Name] = newEvent;
                    existingTime = true;
                    existingPerson = true;
                    PromptDialog.Text(context, After_NamePrompt, "What would you like to call this event?");
                }
                else if ((result.TryFindEntity(Entity_Event_Name, out name)) && (result.TryFindEntity(Entity_Event_DateTime, out time)) && (!result.TryFindEntity(Entity_Event_Person_Name, out person)))
                {
                    //Create the new event object
                    var newEvent = new Event() { Name = name.Entity, Time = time.Entity };
                    //Add the new event to the list of events and also save it in order to add content to it later
                    eventToCreate = this.eventByTitle[newEvent.Name] = newEvent;
                    existingTime = true;
                    existingName = true;
                    PersonQueryPrompt(context);
                }
                else if ((result.TryFindEntity(Entity_Event_Name, out name)) && (result.TryFindEntity(Entity_Event_DateTime, out time)) && (result.TryFindEntity(Entity_Event_Person_Name, out person)))
                {
                    //Create the new event object
                    var newEvent = new Event() { Name = name.Entity, Time = time.Entity, PersonName = person.Entity };
                    //Add the new event to the list of events and also save it in order to add content to it later
                    eventToCreate = this.eventByTitle[newEvent.Name] = newEvent;
                    existingTime = true;
                    existingName = true;
                    DisplayEvent(context);

                }

                return Task.CompletedTask;
            }
        }
        //This task deals with adding the time to the event
        private async Task After_NamePrompt(IDialogContext context, IAwaitable<string> result)
        {
            EntityRecommendation name;
            //Set the title (used for creation, deletion and reading back)
            currentName = await result;
            if ((currentName != null) || (existingName == false))
            {
                name = new EntityRecommendation(type: Entity_Event_Name) { Entity = currentName };
            }
            else
            {
                //Use the default
                name = new EntityRecommendation(type: Entity_Event_Name) { Entity = DefaultEventName };
            }

            if (!existingTime)
            {

                if (!existingName)
                {
                    eventToCreate = this.eventByTitle[DefaultEventName];
                    eventToCreate.Name = name.Entity;
                }
                else
                {
                    //Create the new event object
                    var newEvent = new Event() { Name = name.Entity };
                    //Add the new event to the list of events and also save it in order to add content to it later
                    eventToCreate = this.eventByTitle[newEvent.Name] = newEvent;
                }
                

                //Ask the user when they want to schedule the event.
                PromptDialog.Text(context, After_TimePrompt, "When would you like to schedule this event?");
            }
            else
            {

                if (!existingName)
                {
                    eventToCreate = this.eventByTitle[DefaultEventName];
                    eventToCreate.Name = name.Entity;

                    if (!existingPerson)
                    {
                        await PersonQueryPrompt(context);
                    }
                    else
                    {
                        await DisplayEvent(context);
                    }
                }
                else
                {
                    //Create the new event object
                    var newEvent = new Event() { Name = name.Entity };
                    //Add the new event to the list of events and also save it in order to add content to it later
                    eventToCreate = this.eventByTitle[newEvent.Name] = newEvent;
                    await DisplayEvent(context);
                }

                
            }     
                  
        }
        public enum PersonQueryOptions
        {
           Yes,
           No
        }

        public virtual async Task PersonQueryPrompt(IDialogContext context)
        {

            PromptDialog.Choice(
                context: context,
                resume: ChoiceReceivedAsync,
                options: (IEnumerable<PersonQueryOptions>)Enum.GetValues(typeof(PersonQueryOptions)),
                prompt: "Would you like to associate this event when someone?",
                retry: "Selected option not avilable . Please try again.",
                promptStyle: PromptStyle.Auto
                );
        }

        public virtual async Task ChoiceReceivedAsync(IDialogContext context, IAwaitable<PersonQueryOptions> activity)
        {
            PersonQueryOptions response = await activity;
            if (response == PersonQueryOptions.No)
            {
                await DisplayEvent(context);
            }
            else
            {
                PromptDialog.Text(context, After_PersonNamePrompt, "What's this person's name?");
            }
        }

        private async Task After_PersonQueryPrompt(IDialogContext context, IAwaitable<string> result)
        {
            eventToCreate.PersonName = await result;

            await DisplayEvent(context);
        }

        private async Task After_TimePrompt(IDialogContext context, IAwaitable<string> result)
        {
            //Set the time of the event           
            eventToCreate.Time = await result;

            if (eventToCreate.PersonName == null)
            {
                PersonQueryPrompt(context);
            }
            else
            {
                await DisplayEvent(context);
            }
            
        }

        private async Task After_PersonNamePrompt(IDialogContext context, IAwaitable<string> result)
        {
            eventToCreate.PersonName = await result;
            await DisplayEvent(context);
        }

        private async Task DisplayEvent(IDialogContext context)
        {
            string username;
            string message = "";
            if (context.PrivateConversationData.TryGetValue<string>("Username", out username))
            {
                message = $"Ok { username}, I've created the event ";
            }
            else
            {
                message = "I've created the event ";
            }            
            
            if (this.eventToCreate.PersonName == null)
            {
                message = message + $"**{ this.eventToCreate.Name}** which is scheduled for {this.eventToCreate.Time}.";
            }
            else
            {
                message = message + $"**{ this.eventToCreate.Name}** which is scheduled for {this.eventToCreate.Time} with {this.eventToCreate.PersonName}.";
            }
            await context.PostAsync(message);
            context.Wait(MessageReceived);
        }

        //Thank you
        [LuisIntent("ThankYou")]
        public async Task ThankYouIntent(IDialogContext context, LuisResult result)
        {
            if (result.TopScoringIntent.Score < 0.6)
            {
                None(context);
            }
            else
            {

                string username;
                if (context.PrivateConversationData.TryGetValue<string>("Username", out username))
                {
                    await context.PostAsync($"Always happy to help {username}!");
                }
                else
                {
                    await context.PostAsync("Always happy to help!");

                }

                context.Wait(MessageReceived);
            }
        }

        //List all events
        [LuisIntent("Event.GetAll")]
        public async Task GetAllEventsIntent(IDialogContext context, LuisResult result)
        {
            if (result.TopScoringIntent.Score < 0.4)
            {
                None(context);
            }
            else
            {

                if (eventByTitle.Count < 1)
                {
                    await context.PostAsync("There are currently no events scheduled. Ask me to create one for you!");
                }
                else
                {
                    string EventList = "Here's the list of all your events: \n\n";
                    foreach (KeyValuePair<string, Event> entry in eventByTitle)
                    {
                        Event eventInList = entry.Value;
                        EventList += $"**{eventInList.Name}** at {eventInList.Time}.\n\n";
                    }
                    await context.PostAsync(EventList);
                }
                context.Wait(MessageReceived);
            }
        }

        //Fetches a specific event
        [LuisIntent("Event.Get")]
        public async Task GetEventIntent(IDialogContext context, LuisResult result)
        {
            if (result.TopScoringIntent.Score < 0.6)
            {
                None(context);
            }
            else
            {

                Event foundEvent;
                if (TryFindEvent(result, out foundEvent))
                {
                    await context.PostAsync($"The event **{foundEvent.Name}** is scheduled for {foundEvent.Time}");
                }
                else if (eventByTitle.Count >= 1)
                {
                    await context.PostAsync("Sorry, I couldn't find the event you asked for... Please check the spelling and try again.");
                }
                else
                {
                    await context.PostAsync("There aren't any events scheduled. Create an event and then ask me again! :)");
                }
                context.Wait(MessageReceived);
            }
        }

        //Deletes an event
        [LuisIntent("Event.Delete")]
        public async Task DeleteEventIntent(IDialogContext context, LuisResult result)
        {
            if (result.TopScoringIntent.Score < 0.6)
            {
                None(context);
            }
            else
            {

                Event eventToDelete;
                if (eventByTitle.Count < 1)
                {
                    await context.PostAsync("I don't have any events to delete. Ask me to create one :)");
                    context.Wait(MessageReceived);
                }
                else if (TryFindEvent(result, out eventToDelete))
                {
                    this.eventByTitle.Remove(eventToDelete.Name);
                    await context.PostAsync($"Event **{eventToDelete.Name}** deleted.");
                }
                else
                {
                    //If you can find the name of the event, ask the user for it
                    PromptDialog.Text(context, After_DeleteTitlePrompt, "What is the name of the event you want to delete?");
                }
            }
        }

        private async Task After_DeleteTitlePrompt(IDialogContext context, IAwaitable<string> result)
        {
            Event eventToDelete;
            string nameToDelete = await result;
            bool foundEvent = this.eventByTitle.TryGetValue(nameToDelete, out eventToDelete);

            if (foundEvent)
            {
                this.eventByTitle.Remove(eventToDelete.Name);
                await context.PostAsync($"Event **{eventToDelete.Name}** deleted.");
            }
            else
            {
                await context.PostAsync($"Sorry, I didn't find the event name {nameToDelete}. Please check the spelling and try again :)");
            }

            context.Wait(MessageReceived);
        }

        //Help
        [LuisIntent("OnDevice.Help")]
        public async Task HelpIntent(IDialogContext context, LuisResult result)
        {
            if (result.TopScoringIntent.Score < 0.6)
            {
                None(context);
            }
            else
            {

                await context.PostAsync($"Here's a list of things I can do: \n\n" +
                "- Create an event for you \n\n" +
                "- Tell you what time an event is scheduled to happen \n\n" +
                "- Show you a list of all the events you have scheduled \n\n" +
                "- Delete an event for you \n\n" +
                "- Tell you what the time is \n\n\n" +
                "What can I do for you? :)");

                context.Wait(MessageReceived);
            }
        }


        public bool TryFindEvent(string eventName, out Event retrivedEvent)
        {
            bool foundEvent = this.eventByTitle.TryGetValue(eventName, out retrivedEvent);
            return foundEvent;
        }

        public bool TryFindEvent(LuisResult result, out Event retrivedEvent)
        {
            retrivedEvent = null;
            string nameToFind;

            EntityRecommendation name;
            if (result.TryFindEntity(Entity_Event_Name, out name))
            {
                nameToFind = name.Entity;
            }
            else
            {
                nameToFind = DefaultEventName;
            }

            return this.eventByTitle.TryGetValue(nameToFind, out retrivedEvent);//Returns false if no event is found.
        }

        [LuisIntent("")]
        public async Task None(IDialogContext context, LuisResult result)
        {
            None(context);    
        }        
        
        private async Task None(IDialogContext context)
        {
            string message = "Sorry, I didn't get that. Try again or ask me for help.";
            await context.PostAsync(message);
            context.Wait(MessageReceived);
        }
        
    }

}