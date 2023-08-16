using System.Runtime.ExceptionServices;

using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

Dictionary<long, Message> _pending = new();

var bot = new TelegramBotClient(Environment.GetEnvironmentVariable("TOKEN")!);
bot.StartReceiving(OnUpdate, OnPollingError, new ReceiverOptions() {
  ThrowPendingUpdates = true,
  AllowedUpdates = new UpdateType[] {
    UpdateType.ChatMember,
    UpdateType.CallbackQuery,
  }
});

await Task.Delay(Timeout.Infinite);

Task OnUpdate(ITelegramBotClient bot, Update update, CancellationToken ct)
{
  switch (update.Type)
  {
    case UpdateType.ChatMember: return OnChatMember(bot, update.ChatMember!, ct);
    case UpdateType.CallbackQuery: return OnCallbackQuery(bot, update.CallbackQuery!, ct);
    default: throw new ArgumentOutOfRangeException(nameof(update.Type));
  }
}
async Task OnChatMember(ITelegramBotClient bot, ChatMemberUpdated chatMember, CancellationToken ct)
{
  if (chatMember.From.Id == bot.BotId) return;

  var chat = chatMember.Chat;

  if (chatMember.NewChatMember is ChatMemberMember newChatMember)
  {
    var user = newChatMember.User;

    if (user.IsBot) return;

    await bot.RestrictChatMemberAsync(chat, user.Id, new ChatPermissions() {
      CanSendMessages = false,
      CanSendAudios = false,
      CanSendDocuments = false,
      CanSendPhotos = false,
      CanSendVideos = false,
      CanSendVideoNotes = false,
      CanSendVoiceNotes = false
    });

    var captchaMessage = await bot.SendTextMessageAsync(
      chat, $"[{user.FirstName}](tg://user?id={user.Id}), чей Крым?",
      parseMode: ParseMode.Markdown,
      replyMarkup: new InlineKeyboardMarkup(new InlineKeyboardButton[] {
        InlineKeyboardButton.WithCallbackData("Украины"),
        InlineKeyboardButton.WithCallbackData("России"),
      })
    );
    _pending.Add(user.Id, captchaMessage);
  }
  else if (chatMember.NewChatMember is ChatMemberLeft chatMemberLeft)
  {
    var user = chatMemberLeft.User;

    if (!_pending.TryGetValue(user.Id, out var captchaMessage)) return;

    await bot.DeleteMessageAsync(captchaMessage.Chat, captchaMessage.MessageId);
    _pending.Remove(user.Id);
  }
}
async Task OnCallbackQuery(ITelegramBotClient bot, CallbackQuery callbackQuery, CancellationToken ct)
{
  await bot.AnswerCallbackQueryAsync(callbackQuery.Id);

  var user = callbackQuery.From;

  if (!_pending.TryGetValue(user.Id, out var captchaMessage)) return;

  var chat = captchaMessage.Chat;

  await bot.DeleteMessageAsync(captchaMessage.Chat, captchaMessage.MessageId);
  _pending.Remove(user.Id);

  if (callbackQuery.Data == "Украины")
  {
    await bot.RestrictChatMemberAsync(chat, user.Id, new ChatPermissions() {
      CanSendMessages = true,
      CanSendAudios = true,
      CanSendDocuments = true,
      CanSendPhotos = true,
      CanSendVideos = true,
      CanSendVideoNotes = true,
      CanSendVoiceNotes = true,
      CanSendPolls = true,
      CanSendOtherMessages = true,
      CanAddWebPagePreviews = true,
      CanChangeInfo = true,
      CanInviteUsers = true,
      CanPinMessages = true,
      CanManageTopics = true
    });
  }
  else if (callbackQuery.Data == "России")
  {
    await bot.BanChatMemberAsync(chat, user.Id);
  }
}

static async Task OnPollingError(ITelegramBotClient bot, Exception error, CancellationToken ct)
{
  ExceptionDispatchInfo.Throw(error);
}