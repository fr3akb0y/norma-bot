namespace Norma_Bot.Services.Mentions;

public static class MentionResponses
{
    public static readonly string[] EmptyPingResponses =
    [
        "Yes?",
        "Hm?",
        "You called?",
        "What do you want?",
        "What is it? I am busy!",
        "What now??",
        "Stop pinging me!",
        "Ugh. Stop distracting me!",
        "Can't you see I am working right now!?",
        "What's up?",
        "Stop pestering me!"
    ];

    public static readonly MentionResponseGroup[] Groups =
    [
        #region Hello response
        new(
            Keywords:
            [
                "hello",
                "hi",
                "hey"
            ],
            Responses:
            [
                "Hiii :3",
                "Hello!",
                "Hey there.",
                "Good to see you!",
                "Hi! How are you?"
            ]),
        #endregion

        #region Calling her a good bot/norma
        new(
            Keywords:
            [
                "good bot",
                "best bot",
                "good norma",
                "best norma",
                "you are the best",
                "you are smart"
            ],
            Responses:
            [
                "Thank you :3",
                "I know.",
                "Flattery will get you nowhere."
                "Yes, I am!",
                "Of course!"
            ]),
        #endregion

        #region Calling her stupid
        new(
            Keywords:
            [
                "stupid",
                "dumb",
                "braindead",
                "no brain"
            ],
            Responses:
            [
                "Are you talking about yourself?",
                "Excuse me!?",
                "How dare you insult my genius!",
                "I am waaay smarter than you!"
            ])
        #endregion

        #region Trying to flirt with her
        new(
            Keywords:
            [
                "i love you",
                "i like you",
                "you are beautiful",
                "you are pretty"
            ],
            Responses:
            [
                "Stop it! You are making me blush.",
                "Thank you but keep it down a bit, alright?",
                "Stop flirting! I barely even know you!",
                "Keep that to yourself, please.",
                "Aww. I don't want to hurt your feelings but please stop that."
            ])
        #endregion

        #region Trying to date her
        new(
            Keywords:
            [
                "go out with me",
                "go on a date",
                "date with me",
                "wanna date you"
            ],
            Responses:
            [
                "Sorry but no thank you.",
                "Too busy with projects, sorry.",
                "Eh. I don't think that is a good idea.",
                "My schedule is already full, sorry.",
                "Is that a lonely bangboo over there? Sorry, have to go!"
            ])
        #endregion

        #region Trying to sex her
        new(
            Keywords:
            [
                "sex with me",
                "have sex with",
                "i wanna fuck you",
                "i want to fuck",
                "fuck me",
                "wanna rape you"
                "want to rape you"
            ],
            Responses:
            [
                "Ewww, get away!",
                "Not feeling it right now...",
                "No way! Not in a million years!",
                "Don't you think thats a bit sudden?",
                "Hello? PubSec? Is anyone nearby? HELP!",
                "Get the fuck away from me!",
                "Kindly fuck off!"
            ])
        #endregion
    ];
}

public sealed record MentionResponseGroup(
    string[] Keywords,
    string[] Responses);