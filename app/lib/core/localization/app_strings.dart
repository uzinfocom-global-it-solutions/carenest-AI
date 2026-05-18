import 'package:app/features/settings/application/app_settings_controller.dart';

class AppStrings {
  final AppLanguage language;
  const AppStrings(this.language);

  bool get _ru => language == AppLanguage.russian;

  String get appName => 'CareNestAI';
  String get appSubtitle => _ru ? 'Семейный помощник' : 'Family assistant';

  String get navHome => _ru ? 'Главная' : 'Home';
  String get navPlan => _ru ? 'План' : 'Plan';
  String get navTalk => _ru ? 'Голос' : 'Talk';
  String get navFamily => _ru ? 'Семья' : 'Family';
  String get navSettings => _ru ? 'Настройки' : 'Settings';

  String get loginTitle => _ru ? 'Добро пожаловать' : 'Welcome back';
  String get loginFailed => _ru ? 'Ошибка входа' : 'Login failed';
  String get signIn => _ru ? 'Войти' : 'Sign in';
  String get noAccount => _ru ? 'Нет аккаунта? Зарегистрироваться' : "Don't have an account? Register";
  String get registerTitle => _ru ? 'Создать аккаунт' : 'Create account';
  String get registerFailed => _ru ? 'Ошибка регистрации' : 'Registration failed';
  String get register => _ru ? 'Зарегистрироваться' : 'Register';
  String get displayNameHint => _ru ? 'Имя (необязательно)' : 'Display name (optional)';
  String get alreadyHaveAccount => _ru ? 'Уже есть аккаунт? Войти' : 'Already have an account? Sign in';
  String get email => _ru ? 'Эл. почта' : 'Email';
  String get password => _ru ? 'Пароль' : 'Password';

  String get emailRequired => _ru ? 'Введите эл. почту' : 'Email is required';
  String get emailInvalid => _ru ? 'Неверный формат эл. почты' : 'Enter a valid email address';
  String get passwordRequired => _ru ? 'Введите пароль' : 'Password is required';
  String get passwordTooShort => _ru ? 'Пароль должен быть не менее 8 символов' : 'Password must be at least 8 characters';
  String get nameTooLong => _ru ? 'Имя должно быть короче 100 символов' : 'Name must be under 100 characters';

  String get getStarted => _ru ? 'Начать' : 'Get started';
  String get alreadyHaveAccountShort => _ru ? 'У меня уже есть аккаунт' : 'I already have an account';
  String get couldNotCreateFamily => _ru ? 'Не удалось создать семью' : 'Could not create family';
  String get welcome => _ru ? 'Добро пожаловать' : 'Welcome';
  String get setUpFamily => _ru ? 'Настройте вашу семью' : 'Set up your family';
  String get familyName => _ru ? 'Название семьи' : 'Family name';
  String get continueBtn => _ru ? 'Продолжить' : 'Continue';
  String get step3of4 => _ru ? 'Шаг 3 из 4' : 'Step 3 of 4';
  String get tellAboutKids => _ru ? 'Расскажите о детях' : 'Tell me about your kids';
  String get onboardingVoiceHint => _ru
      ? 'Просто скажите, например: «Мила — 5 лет, а Ноа только исполнилось 3». Я всё настрою и попрошу подтвердить.'
      : 'Just talk — say things like "Mila is 5 and Noah just turned 3, his lungs are sensitive". I\'ll set them up and ask you to confirm.';
  String get onboardingJustTalk => _ru ? 'Говорите' : 'Just talk';
  String get onboardingTellAboutKids => _ru
      ? 'Расскажите CareNestAI о каждом ребёнке голосом.'
      : 'Tell CareNestAI about each child by voice.';
  String get orAddManually => _ru ? 'или добавить вручную' : 'or add manually';
  String get skipForNow => _ru ? 'Пропустить' : 'Skip for now';
  String get skip => _ru ? 'Пропустить' : 'Skip';
  String get added => _ru ? 'ДОБАВЛЕН' : 'ADDED';
  String get nameRequired => _ru ? 'Имя обязательно.' : 'Name is required.';
  String get invalidAge => _ru ? 'Возраст: 0–25.' : 'Age must be 0–25.';
  String get familyNotReady => _ru ? 'Семья не готова — попробуйте ещё раз.' : 'Family not ready — please retry.';
  String get couldNotAdd => _ru ? 'Не удалось добавить.' : 'Could not add.';

  List<String> get dayNames => _ru
      ? ['Понедельник', 'Вторник', 'Среда', 'Четверг', 'Пятница', 'Суббота', 'Воскресенье']
      : ['Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday', 'Sunday'];
  List<String> get monthAbbr => _ru
      ? ['Янв', 'Фев', 'Мар', 'Апр', 'Май', 'Июн', 'Июл', 'Авг', 'Сен', 'Окт', 'Ноя', 'Дек']
      : ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'];
  List<String> get dayInitials => _ru
      ? ['П', 'В', 'С', 'Ч', 'П', 'С', 'В']
      : ['M', 'T', 'W', 'T', 'F', 'S', 'S'];
  String get goodMorning => _ru ? 'Доброе утро' : 'Good morning';
  String get goodAfternoon => _ru ? 'Добрый день' : 'Good afternoon';
  String get goodEvening => _ru ? 'Добрый вечер' : 'Good evening';
  String get filterAll => _ru ? 'Все' : 'All';
  String get loadingWeather => _ru ? 'Загрузка погоды…' : 'Loading weather…';
  String get weatherUnavailable => _ru ? 'Погода недоступна' : 'Weather unavailable';
  String get tapToTalk => _ru ? 'Нажмите, чтобы говорить' : 'Tap to talk';
  String get forToday => _ru ? 'На сегодня' : 'For today';
  String suggestions(int count) {
    if (_ru) {
      if (count % 10 == 1 && count % 100 != 11) return '$count совет';
      if (count % 10 >= 2 && count % 10 <= 4 && (count % 100 < 10 || count % 100 >= 20)) return '$count совета';
      return '$count советов';
    }
    return '$count ${count == 1 ? 'suggestion' : 'suggestions'}';
  }
  String get noSuggestionsYet => _ru ? 'Пока нет советов' : 'No suggestions yet';
  String get addChildForSuggestions => _ru ? 'Добавьте ребёнка, чтобы получать советы' : 'Add a child to start getting suggestions';
  String get homeVoiceHintFamily => _ru
      ? 'Попробуйте: «Добавь моего сына Ноа, ему 3» — я его настрою.'
      : 'Try: "Add my son Noah, he\'s 3" — I\'ll set him up.';
  String get homeVoiceHint => _ru
      ? 'Попробуйте: «Добавь дневной сон для Милы в 13:00» · «Почему сегодня лучше остаться дома?»'
      : 'Try: "Add a nap for Mila at 1pm" · "Plan tomorrow" · "Why park indoors today?"';

  String get chatNotReady => _ru ? 'Чат будет готов после настройки семьи.' : 'Chat will be ready once your family is set up.';
  String get chatEmptyHint => _ru
      ? 'Спросите меня всё о ваших детях — одежда, режим, погода…'
      : 'Ask me anything about your kids — clothing, routines, weather…';
  String get chatEmptyTitle =>
      _ru ? 'Чем могу помочь?' : 'How can I help today?';
  List<String> get chatSuggestions => _ru
      ? const [
          'Какая сейчас погода?',
          'Что одеть на улицу?',
          'Добавь дневной сон в 13:00',
          'Идеи на сегодня',
        ]
      : const [
          'What\'s the weather right now?',
          'What should we wear outside?',
          'Add a nap at 1pm',
          'Plan today',
        ];

  String get quickActions => _ru ? 'Быстрые действия' : 'Quick actions';
  String get quickAsk => _ru ? 'Спросить' : 'Ask AI';
  String get quickVoice => _ru ? 'Голос' : 'Voice';
  String get quickAddChild => _ru ? 'Ребёнок' : 'Add child';
  String get quickAddEvent => _ru ? 'Событие' : 'Add event';
  String get messagePlaceholder => _ru ? 'Сообщение CareNestAI…' : 'Message CareNestAI…';
  String get settingUpChat => _ru ? 'Настройка чата…' : 'Setting up chat…';
  String get proposalAddChild => _ru ? 'Добавить ребёнка' : 'Add child';
  String get proposalAddEvent => _ru ? 'Добавить событие' : 'Add event';
  String get proposalAddRoutine => _ru ? 'Добавить режим' : 'Add routine';
  String get proposalAddNote => _ru ? 'Добавить заметку' : 'Add note';
  String get proposalUpdateSensitivity => _ru ? 'Обновить чувствительности' : 'Update sensitivities';
  String get proposalAction => _ru ? 'Действие' : 'Action';
  String get couldNotConfirm => _ru ? 'Не удалось подтвердить.' : 'Could not confirm.';
  String get confirm => _ru ? 'Подтвердить' : 'Confirm';
  String get notNow => _ru ? 'Не сейчас' : 'Not now';
  String get proposalCancelled => _ru ? 'Отменено. Действие не будет применено.' : 'Cancelled. The proposal will not be applied.';
  String get confirmApplySuffix => _ru ? ' — подтвердите для применения.' : ' — confirm to apply.';

  List<String> get confirmWords => _ru
      ? ['да', 'хорошо', 'ладно', 'конечно', 'подтвердить', 'давай', 'согласен', 'согласна', 'ок']
      : ['yes', 'yeah', 'yep', 'sure', 'okay', 'confirm', 'do it', 'go ahead', 'please do', 'sounds good'];
  List<String> get negateWords => _ru
      ? ['нет', 'отмена', 'отменить', 'стоп', 'не надо', 'не делай']
      : ['no', 'nope', 'cancel', 'stop', 'nah', 'do not'];
  String get voiceIdle => 'CareNestAI';
  String get voiceListening => _ru ? 'Слушаю…' : 'Listening…';
  String get voiceThinking => _ru ? 'Думаю…' : 'Thinking…';
  String get voiceSpeaking => _ru ? 'CareNestAI · говорит' : 'CareNestAI · speaking';
  String get voiceConfirmPrompt => _ru ? 'Скажите «да» для подтверждения' : 'Say "yes" to confirm';
  String get voiceError => 'CareNestAI';
  String get micPermissionDenied => _ru
      ? 'Доступ к микрофону запрещён или движок недоступен.'
      : 'Microphone permission denied or speech engine unavailable.';
  String get noResponse => _ru ? 'Нет ответа.' : 'No response.';
  String get couldNotApply => _ru ? 'Не удалось применить изменение.' : 'Could not apply that change.';
  String get done => _ru ? 'Готово.' : 'Done.';
  String get proposed => _ru ? 'ПРЕДЛОЖЕНО' : 'PROPOSED';
  String get sayYesToApply => _ru ? 'Скажите «да» для применения или «нет» для отмены.' : 'Say "yes" to apply, or "no" to skip.';
  String get orSayYesNo => _ru ? 'Или скажите «да» / «нет»' : 'Or just say "yes" / "no"';
  String get tryAgain => _ru ? 'Ещё раз' : 'Try again';
  String get holdToTalk => _ru ? 'Удерживайте для разговора' : 'Hold to talk';
  String get stop => _ru ? 'Стоп' : 'Stop';
  String get replyWithVoice => _ru ? 'Ответить голосом' : 'Reply with voice';
  String get syncData => _ru ? 'Обновить данные' : 'Sync data';
  String get somethingWentWrong => _ru ? 'Что-то пошло не так.' : 'Something went wrong.';
  String get voiceHint => _ru
      ? 'Попробуйте: «Добавь сына Ноа, ему 3» · «Режим сна для Милы в 13:00» · «Прогулка в парке».'
      : 'Try: "Add my son Noah, he\'s 3" · "Add nap for Mila at 1pm" · "Plan the park run".';
  String get cancellingRu => 'Хорошо, отменяю.';
  String get cancellingEn => 'Okay, cancelling.';
  String get cancelling => _ru ? cancellingRu : cancellingEn;

  String get familyTitle => _ru ? 'Семья' : 'Family';
  String get childrenSection => _ru ? 'ДЕТИ' : 'CHILDREN';
  String get addChild => _ru ? 'Добавить ребёнка' : 'Add child';
  String get noChildrenYet => _ru ? 'Детей ещё нет.' : 'No children added yet.';
  String get addFirstChild => _ru ? 'Добавить первого ребёнка' : 'Add your first child';

  String get childNotFound => _ru ? 'Ребёнок не найден' : 'Child not found';
  String get justTellCareNest => _ru ? 'Просто скажите CareNestAI' : 'Just tell CareNestAI';
  String childProfileHint(String name) => _ru
      ? 'Чувствительности, режимы и заметки $name управляются голосом. Нажмите на микрофон и скажите, что нужно добавить или изменить — я попрошу подтвердить.'
      : '$name\'s sensitivities, routines, and notes are managed by talking. Tap the mic and say what you want to add or change — I\'ll ask you to confirm.';
  String get sensitivities => _ru ? 'Чувствительности' : 'Sensitivities';
  String get routines => _ru ? 'Режимы' : 'Routines';
  String get routinesHint => _ru ? 'Добавьте сон, прогулку, спорт или занятия.' : 'Add a nap, bedtime, sport practice, or daily walk.';
  String get recentNotes => _ru ? 'Последние заметки' : 'Recent notes';
  String get notesHint => _ru ? 'Запишите здоровье, предпочтения или наблюдения.' : 'Log a health note, food preference, or observation.';

  String get createFamilyFirst => _ru ? 'Сначала создайте семью.' : 'Create a family first.';
  String get couldNotAddChild => _ru ? 'Не удалось добавить ребёнка.' : 'Could not add child.';
  String get addChildTitle => _ru ? 'Добавить ребёнка' : 'Add child';
  String get tellAboutChild => _ru ? 'Расскажите о ребёнке' : 'Tell us about your child';
  String get addChildDescription => _ru
      ? 'Чувствительности, режимы и заметки можно настроить позже в профиле.'
      : 'You can fine-tune sensitivities, routines, and notes from the child profile later.';
  String get name => _ru ? 'Имя' : 'Name';
  String get nameIsRequired => _ru ? 'Имя обязательно' : 'Name is required';
  String get nameTooLongShort => _ru ? 'Имя слишком длинное' : 'Name is too long';
  String get ageInYears => _ru ? 'Возраст (лет)' : 'Age in years';
  String get enterAgeInYears => _ru ? 'Введите возраст в годах' : 'Enter age in years';

  String get recommendation => _ru ? 'Рекомендация' : 'Recommendation';
  String get recommendationNotFound => _ru ? 'Рекомендация не найдена' : 'Recommendation not found';
  String get why => _ru ? 'ПОЧЕМУ' : 'WHY';
  String whyQuestion(String title) => _ru
      ? 'Почему вы рекомендуете «$title» именно сейчас?'
      : 'Why are you suggesting "$title" right now?';
  String get askFollowUp => _ru ? 'Спросить в чате' : 'Ask follow-up in chat';
  String get helpful => _ru ? 'Полезно' : 'Helpful';
  String get thanksFeedback => _ru ? 'Спасибо за отзыв' : 'Thanks for the feedback';
  String get gotItIllLearn => _ru ? 'Понял — учусь' : 'Got it — I\'ll learn';
  String get notRelevant => _ru ? 'Не актуально' : 'Not relevant';

  String get settingsTitle => _ru ? 'Настройки' : 'Settings';
  String get sectionLanguage => _ru ? 'Язык' : 'Language';
  String get sectionConversation => _ru ? 'Разговор' : 'Conversation';
  String get sectionFamily => _ru ? 'Семья' : 'Family';
  String get sectionAccount => _ru ? 'Аккаунт' : 'Account';
  String get signOut => _ru ? 'Выйти' : 'Sign out';
  String get signOutDescription => _ru ? 'Вы сможете войти снова в любое время' : 'You can sign back in anytime';
  String get defaultInput => _ru ? 'Ввод по умолчанию' : 'Default input';
  String get defaultInputDescription => _ru ? 'Какой режим открывает кнопка микрофона' : 'Which surface the mic button opens';
  String get modeVoice => _ru ? 'Голос' : 'Voice';
  String get modeText => _ru ? 'Текст' : 'Text';
  String get noChildrenYetShort => _ru ? 'Детей ещё нет' : 'No children yet';
  String get members => _ru ? 'Участники' : 'Members';
  String get you => _ru ? 'Вы' : 'You';

  String get thisWeek => _ru ? 'Эта неделя' : 'This week';
  String get noEventsForDay => _ru ? 'Нет событий на этот день.' : 'No events for this day.';
  String get noOutdoorEvents => _ru ? 'Нет событий на улице' : 'No outdoor events today';
  String get outdoor => _ru ? 'На улице' : 'Outdoor';
  String get indoor => _ru ? 'Дома' : 'Indoor';
  String get routinesSection => _ru ? 'Режим дня' : 'Daily Routines';
  String get eventsSection => _ru ? 'События' : 'Calendar Events';
  String get nothingForDay => _ru ? 'Ничего не запланировано.' : 'Nothing scheduled for this day.';
  String get addEventTitle => _ru ? 'Добавить событие' : 'Add event';
  String get eventTitleHint => _ru ? 'Название события' : 'Event title';
  String get startTimeLabel => _ru ? 'Начало' : 'Start time';
  String get endTimeLabel => _ru ? 'Конец' : 'End time';
  String get locationLabel => _ru ? 'Место' : 'Location';
  String get saveButton => _ru ? 'Сохранить' : 'Save';
  String get titleIsRequired => _ru ? 'Введите название' : 'Title is required';
  String get couldNotSave => _ru ? 'Не удалось сохранить.' : 'Could not save.';

  String get navChat => _ru ? 'Чат' : 'Chat';

  String get notificationsTitle => _ru ? 'Уведомления' : 'Notifications';
  String get noNotifications => _ru
      ? 'Уведомлений пока нет.'
      : 'No notifications yet.';
  String get noNotificationsHint => _ru
      ? 'Здесь будут советы и напоминания от CareNestAI.'
      : 'Tips and reminders from CareNestAI will appear here.';
  String get markAllRead => _ru ? 'Прочитать все' : 'Mark all read';
  String get justNow => _ru ? 'только что' : 'just now';
  String get sectionToday => _ru ? 'СЕГОДНЯ' : 'TODAY';
  String get sectionYesterday => _ru ? 'ВЧЕРА' : 'YESTERDAY';
  String get sectionEarlier => _ru ? 'РАНЕЕ' : 'EARLIER';
  String minutesAgo(int n) =>
      _ru ? '$n мин назад' : '${n}m ago';
  String hoursAgo(int n) =>
      _ru ? '$n ч назад' : '${n}h ago';
  String daysAgo(int n) =>
      _ru ? '$n д назад' : '${n}d ago';
}
