import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import 'package:provider/provider.dart';
import 'package:app/shared/theme/app_colors.dart';
import 'package:app/shared/theme/app_text_styles.dart';
import 'package:app/shared/theme/app_spacing.dart';
import 'package:app/features/chats/application/chat_controller.dart';
import 'package:app/features/chats/application/proposal_refresh.dart';
import 'package:app/features/chats/domain/chat_models.dart';
import 'package:app/features/notifications/application/voice_notification_controller.dart';
import 'package:app/features/notifications/data/notification_deep_link_handler.dart';
import 'package:app/features/settings/application/app_settings_controller.dart';
import 'package:app/features/voice/data/voice_service.dart';
import 'package:app/core/localization/app_strings.dart';

class ChatScreen extends StatefulWidget {
  const ChatScreen({super.key});

  @override
  State<ChatScreen> createState() => _ChatScreenState();
}

class _ChatScreenState extends State<ChatScreen> with WidgetsBindingObserver {
  final _scrollController = ScrollController();
  final _inputController = TextEditingController();

  int? _highlightedMessageId;

  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addObserver(this);
    WidgetsBinding.instance.addPostFrameCallback((_) {
      _scrollToBottom();
      final pendingId =
          NotificationDeepLinkHandler.instance.pendingScrollToMessageId;
      if (pendingId != null) {
        NotificationDeepLinkHandler.instance.clearPendingScroll();
        WidgetsBinding.instance.addPostFrameCallback(
          (_) => _scrollToMessage(pendingId),
        );
      }
    });
  }

  void _scrollToMessage(int messageId) {
    final messages = context.read<ChatController>().messages;
    final idx = messages.indexWhere((m) => m.id == messageId);
    if (idx == -1 || !_scrollController.hasClients) return;

    final itemHeight = 80.0;
    final offset = (idx * itemHeight).clamp(
      _scrollController.position.minScrollExtent,
      _scrollController.position.maxScrollExtent,
    );
    _scrollController.animateTo(
      offset,
      duration: const Duration(milliseconds: 350),
      curve: Curves.easeInOut,
    );

    setState(() => _highlightedMessageId = messageId);
    Future.delayed(const Duration(seconds: 2), () {
      if (mounted) setState(() => _highlightedMessageId = null);
    });
  }

  @override
  void dispose() {
    WidgetsBinding.instance.removeObserver(this);
    _scrollController.dispose();
    _inputController.dispose();
    super.dispose();
  }

  @override
  void didChangeAppLifecycleState(AppLifecycleState state) {
    if (state == AppLifecycleState.resumed) {
      context.read<ChatController>().refreshOnResume();
    }
  }

  void _scrollToBottom() {
    WidgetsBinding.instance.addPostFrameCallback((_) {
      if (_scrollController.hasClients) {
        _scrollController.animateTo(
          _scrollController.position.maxScrollExtent,
          duration: const Duration(milliseconds: 250),
          curve: Curves.easeOut,
        );
      }
    });
  }

  Future<void> _send(ChatController chatCtrl) async {
    final text = _inputController.text.trim();
    if (text.isEmpty || chatCtrl.isSending) return;
    _inputController.clear();
    _scrollToBottom();
    final locale = context.read<AppSettingsController>().apiLocale;
    await chatCtrl.sendMessage(text, locale: locale);
    if (!mounted) return;
    _scrollToBottom();
  }

  @override
  Widget build(BuildContext context) {
    final chatCtrl = context.watch<ChatController>();
    final strings = AppStrings(context.watch<AppSettingsController>().language);
    final messages = chatCtrl.messages;
    final isReady = chatCtrl.activeChatId != null;

    final hasMessages = messages.isNotEmpty;

    return Scaffold(
      backgroundColor: AppColors.background,
      body: SafeArea(
        bottom: false,
        child: Column(
          children: [
            _Header(),
            Expanded(
              child: chatCtrl.isLoading
                  ? const Center(
                      child: CircularProgressIndicator(
                        color: AppColors.primary,
                        strokeWidth: 2,
                      ),
                    )
                  : !isReady
                  ? Center(
                      child: Padding(
                        padding: const EdgeInsets.all(20),
                        child: Text(
                          chatCtrl.error ??
                              strings.chatNotReady,
                          textAlign: TextAlign.center,
                          style: AppTextStyles.bodySmall.copyWith(
                            color: chatCtrl.error != null
                                ? AppColors.red
                                : AppColors.textTertiary,
                          ),
                        ),
                      ),
                    )
                  : !hasMessages
                  ? _ChatEmpty(
                      onPick: (text) async {
                        _inputController.text = text;
                        await _send(chatCtrl);
                      },
                    )
                  : ListView.separated(
                      controller: _scrollController,
                      padding: const EdgeInsets.fromLTRB(
                        AppSpacing.lg,
                        AppSpacing.sm,
                        AppSpacing.lg,
                        AppSpacing.lg,
                      ),
                      itemCount: messages.length + (chatCtrl.isSending ? 1 : 0),
                      separatorBuilder: (_, _) => const SizedBox(height: 8),
                      itemBuilder: (context, i) {
                        if (i == messages.length && chatCtrl.isSending) {
                          return const _TypingBubble();
                        }
                        final m = messages[i];
                        final isHighlighted = _highlightedMessageId == m.id;
                        if (m.isProactive) {
                          return _ProactiveMessageCard(
                            message: m,
                            highlighted: isHighlighted,
                          );
                        }
                        if (m.isProposal) {
                          return _ProposalCard(message: m);
                        }
                        return _MessageBubble(message: m);
                      },
                    ),
            ),
            if (isReady && chatCtrl.error != null && hasMessages)
              _ErrorBanner(message: chatCtrl.error!),
            _Composer(
              controller: _inputController,
              enabled: isReady && !chatCtrl.isSending,
              onSend: () => _send(chatCtrl),
            ),
          ],
        ),
      ),
    );
  }
}

class _Header extends StatelessWidget {
  @override
  Widget build(BuildContext context) {
    final strings = AppStrings(context.watch<AppSettingsController>().language);
    return Padding(
      padding: const EdgeInsets.fromLTRB(20, 16, 20, 8),
      child: Row(
        children: [
          Container(
            width: 36,
            height: 36,
            decoration: BoxDecoration(
              gradient: const LinearGradient(
                begin: Alignment.topLeft,
                end: Alignment.bottomRight,
                colors: [Color(0xFF5B8AFF), AppColors.primary],
              ),
              borderRadius: BorderRadius.circular(18),
            ),
            child: const Icon(
              Icons.auto_awesome,
              size: 18,
              color: Colors.white,
            ),
          ),
          const SizedBox(width: 10),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(strings.appName, style: AppTextStyles.labelLarge),
                Text(
                  strings.appSubtitle,
                  style: AppTextStyles.caption.copyWith(
                    color: AppColors.textTertiary,
                  ),
                ),
              ],
            ),
          ),
          GestureDetector(
            onTap: () => context.push('/voice'),
            child: Container(
              width: 40,
              height: 40,
              decoration: BoxDecoration(
                shape: BoxShape.circle,
                gradient: const RadialGradient(
                  colors: [
                    Color(0xFF5B8AFF),
                    AppColors.primary,
                    Color(0xFF1D4ED8),
                  ],
                  stops: [0.0, 0.6, 1.0],
                ),
                boxShadow: [
                  BoxShadow(
                    color: AppColors.primary.withValues(alpha: 0.35),
                    blurRadius: 10,
                    offset: const Offset(0, 3),
                  ),
                ],
              ),
              child: const Icon(Icons.mic, color: Colors.white, size: 18),
            ),
          ),
        ],
      ),
    );
  }
}

class _MessageBubble extends StatelessWidget {
  const _MessageBubble({required this.message});
  final ChatMessageModel message;

  @override
  Widget build(BuildContext context) {
    final isUser = message.senderType == 'User';
    final raw = isUser ? message.content : (message.aiResponse ?? message.content);
    final text = raw.startsWith('[proactive] ')
        ? raw.substring('[proactive] '.length)
        : raw;

    Widget? ttsButton;
    if (!isUser) {
      final voice = context.watch<VoiceService>();
      final ttsLocale = context.read<AppSettingsController>().ttsLocale;
      ttsButton = GestureDetector(
        onTap: () {
          if (voice.isSpeaking) {
            voice.stopSpeaking();
          } else {
            voice.speak(text, ttsLocale: ttsLocale);
          }
        },
        child: Padding(
          padding: const EdgeInsets.only(top: 6),
          child: Align(
            alignment: Alignment.centerRight,
            child: Icon(
              voice.isSpeaking
                  ? Icons.stop_circle_outlined
                  : Icons.volume_up_outlined,
              size: 15,
              color: AppColors.textTertiary,
            ),
          ),
        ),
      );
    }

    return Align(
      alignment: isUser ? Alignment.centerRight : Alignment.centerLeft,
      child: Container(
        constraints: BoxConstraints(
          maxWidth: MediaQuery.of(context).size.width * 0.78,
        ),
        padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 10),
        decoration: BoxDecoration(
          gradient: isUser
              ? const LinearGradient(
                  begin: Alignment.topLeft,
                  end: Alignment.bottomRight,
                  colors: [Color(0xFF5B8AFF), Color(0xFF2563FF)],
                )
              : null,
          color: isUser ? null : AppColors.surface,
          borderRadius: BorderRadius.only(
            topLeft: const Radius.circular(16),
            topRight: const Radius.circular(16),
            bottomLeft: Radius.circular(isUser ? 16 : 4),
            bottomRight: Radius.circular(isUser ? 4 : 16),
          ),
          boxShadow: [
            BoxShadow(
              color: isUser
                  ? AppColors.primary.withValues(alpha: 0.22)
                  : Colors.black.withValues(alpha: 0.04),
              blurRadius: isUser ? 12 : 6,
              offset: const Offset(0, 3),
            ),
          ],
        ),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          mainAxisSize: MainAxisSize.min,
          children: [
            Text(
              text,
              style: AppTextStyles.bodySmall.copyWith(
                color: isUser ? Colors.white : AppColors.textPrimary,
                height: 1.4,
              ),
            ),
            ?ttsButton,
          ],
        ),
      ),
    );
  }
}

class _ProposalCard extends StatelessWidget {
  const _ProposalCard({required this.message});
  final ChatMessageModel message;

  String _typeLabel(String type, AppStrings strings) {
    switch (type) {
      case 'add_child':
        return strings.proposalAddChild;
      case 'add_event':
        return strings.proposalAddEvent;
      case 'add_routine':
        return strings.proposalAddRoutine;
      case 'add_note':
        return strings.proposalAddNote;
      case 'update_sensitivity':
        return strings.proposalUpdateSensitivity;
      default:
        return strings.proposalAction;
    }
  }

  IconData _typeIcon(String type) {
    switch (type) {
      case 'add_child':
        return Icons.child_care;
      case 'add_event':
        return Icons.event;
      case 'add_routine':
        return Icons.alarm;
      case 'add_note':
        return Icons.note_alt_outlined;
      case 'update_sensitivity':
        return Icons.tune;
      default:
        return Icons.auto_awesome;
    }
  }

  @override
  Widget build(BuildContext context) {
    final ctrl = context.watch<ChatController>();
    final strings = AppStrings(context.watch<AppSettingsController>().language);
    final p = message.proposal!;
    final isBusy = ctrl.isConfirming && ctrl.confirmingMessageId == message.id;
    final disabled = ctrl.isConfirming;

    return Align(
      alignment: Alignment.centerLeft,
      child: Container(
        constraints: BoxConstraints(
          maxWidth: MediaQuery.of(context).size.width * 0.86,
        ),
        padding: const EdgeInsets.fromLTRB(14, 12, 14, 10),
        decoration: BoxDecoration(
          gradient: const LinearGradient(
            begin: Alignment.topLeft,
            end: Alignment.bottomRight,
            colors: [Color(0xFFEBF0FF), AppColors.blueSoft],
          ),
          borderRadius: const BorderRadius.only(
            topLeft: Radius.circular(16),
            topRight: Radius.circular(16),
            bottomLeft: Radius.circular(4),
            bottomRight: Radius.circular(16),
          ),
          border: Border.all(color: AppColors.primary.withValues(alpha: 0.22)),
          boxShadow: [
            BoxShadow(
              color: AppColors.primary.withValues(alpha: 0.10),
              blurRadius: 12,
              offset: const Offset(0, 4),
            ),
          ],
        ),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              children: [
                Icon(_typeIcon(p.type), size: 16, color: AppColors.primary),
                const SizedBox(width: 6),
                Text(
                  _typeLabel(p.type, strings).toUpperCase(),
                  style: AppTextStyles.labelSmall.copyWith(
                    color: AppColors.primary,
                    letterSpacing: 0.6,
                  ),
                ),
              ],
            ),
            const SizedBox(height: 6),
            Text(
              p.summary,
              style: AppTextStyles.bodyMedium.copyWith(
                color: AppColors.textPrimary,
                height: 1.4,
              ),
            ),
            const SizedBox(height: 12),
            Row(
              children: [
                Expanded(
                  child: GestureDetector(
                    onTap: disabled
                        ? null
                        : () async {
                            final ok =
                                await ctrl.confirmProposal(message.id);
                            if (!context.mounted) return;
                            if (!ok) {
                              ScaffoldMessenger.of(context).showSnackBar(
                                SnackBar(
                                  content: Text(
                                    ctrl.error ?? strings.couldNotConfirm,
                                  ),
                                ),
                              );
                              return;
                            }
                            await refreshAllAfterAiAction(context);
                          },
                    child: Container(
                      height: 40,
                      decoration: BoxDecoration(
                        color: disabled
                            ? AppColors.lineStrong
                            : AppColors.primary,
                        borderRadius: BorderRadius.circular(12),
                      ),
                      alignment: Alignment.center,
                      child: isBusy
                          ? const SizedBox(
                              width: 16,
                              height: 16,
                              child: CircularProgressIndicator(
                                strokeWidth: 2,
                                color: Colors.white,
                              ),
                            )
                          : Row(
                              mainAxisAlignment: MainAxisAlignment.center,
                              children: [
                                const Icon(Icons.check, color: Colors.white, size: 16),
                                const SizedBox(width: 6),
                                Text(
                                  strings.confirm,
                                  style: TextStyle(
                                    color: Colors.white,
                                    fontWeight: FontWeight.w600,
                                  ),
                                ),
                              ],
                            ),
                    ),
                  ),
                ),
                const SizedBox(width: 8),
                GestureDetector(
                  onTap: disabled
                      ? null
                      : () {
                          ScaffoldMessenger.of(context).showSnackBar(
                            SnackBar(
                              content: Text(
                                strings.proposalCancelled,
                              ),
                            ),
                          );
                        },
                  child: Container(
                    height: 40,
                    padding: const EdgeInsets.symmetric(horizontal: 16),
                    decoration: BoxDecoration(
                      color: AppColors.card,
                      borderRadius: BorderRadius.circular(12),
                      border: Border.all(color: AppColors.line),
                    ),
                    alignment: Alignment.center,
                    child: Text(
                      strings.notNow,
                      style: const TextStyle(
                        color: AppColors.textSecondary,
                        fontWeight: FontWeight.w600,
                      ),
                    ),
                  ),
                ),
              ],
            ),
          ],
        ),
      ),
    );
  }
}

class _ProactiveMessageCard extends StatelessWidget {
  const _ProactiveMessageCard({
    required this.message,
    this.highlighted = false,
  });

  final ChatMessageModel message;
  final bool highlighted;

  static const _indigo = Color(0xFF4F46E5);

  IconData _icon() {
    switch (message.proactiveEventType) {
      case 'MorningBriefing':
        return Icons.wb_sunny_outlined;
      case 'MedicationReminder':
        return Icons.medication_outlined;
      case 'WeatherAlert':
        return Icons.cloud_outlined;
      case 'AQIAlert':
        return Icons.air;
      case 'LeaveHomeChecklist':
        return Icons.checklist;
      case 'ProactiveRecommendation':
        return Icons.lightbulb_outline;
      default:
        return Icons.smart_toy_outlined;
    }
  }

  Color _iconColor() {
    switch (message.proactiveEventType) {
      case 'MorningBriefing':
        return const Color(0xFFF59E0B);
      case 'MedicationReminder':
        return const Color(0xFFEF4444);
      case 'WeatherAlert':
        return const Color(0xFF3B82F6);
      case 'AQIAlert':
        return const Color(0xFFF97316);
      case 'LeaveHomeChecklist':
        return const Color(0xFF22C55E);
      default:
        return _indigo;
    }
  }

  String _relativeTime() {
    final ts = message.createdAt;
    if (ts == null) return '';
    final diff = DateTime.now().difference(ts);
    if (diff.inMinutes < 1) return 'только что';
    if (diff.inMinutes < 60) return '${diff.inMinutes} мин назад';
    if (diff.inHours < 24) return '${diff.inHours} ч назад';
    return '${diff.inDays} д назад';
  }

  String _displayText() {
    final raw = message.aiResponse ?? message.content;
    return raw.startsWith('[proactive] ')
        ? raw.substring('[proactive] '.length)
        : raw;
  }

  @override
  Widget build(BuildContext context) {
    final voiceCtrl = context.watch<VoiceNotificationController>();
    final correlationId = message.correlationId;

    VoiceActionModel? linkedAction;
    if (correlationId != null) {
      try {
        linkedAction = voiceCtrl.actions.firstWhere(
          (a) => a.id.toString() == correlationId,
        );
      } catch (_) {
        linkedAction = null;
      }
    }

    final isPending = linkedAction != null &&
        (linkedAction.status == 'Pending' ||
            linkedAction.status == 'Delivered');

    return Align(
      alignment: Alignment.centerLeft,
      child: AnimatedContainer(
        duration: const Duration(milliseconds: 300),
        constraints: BoxConstraints(
          maxWidth: MediaQuery.of(context).size.width * 0.90,
        ),
        decoration: BoxDecoration(
          color: highlighted
              ? _indigo.withValues(alpha: 0.12)
              : AppColors.surface,
          borderRadius: const BorderRadius.only(
            topLeft: Radius.circular(16),
            topRight: Radius.circular(16),
            bottomLeft: Radius.circular(4),
            bottomRight: Radius.circular(16),
          ),
          border: Border(
            left: BorderSide(color: _indigo, width: 3),
          ),
        ),
        child: Padding(
          padding: const EdgeInsets.fromLTRB(12, 10, 12, 10),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Row(
                children: [
                  Icon(_icon(), size: 15, color: _iconColor()),
                  const SizedBox(width: 5),
                  Expanded(
                    child: Text(
                      _eventLabel(),
                      style: AppTextStyles.labelSmall.copyWith(
                        color: _iconColor(),
                        letterSpacing: 0.4,
                      ),
                    ),
                  ),
                  Container(
                    padding: const EdgeInsets.symmetric(
                      horizontal: 6,
                      vertical: 2,
                    ),
                    decoration: BoxDecoration(
                      color: _indigo.withValues(alpha: 0.12),
                      borderRadius: BorderRadius.circular(6),
                    ),
                    child: Text(
                      '🤖 AI',
                      style: AppTextStyles.labelSmall.copyWith(
                        color: _indigo,
                        fontSize: 10,
                      ),
                    ),
                  ),
                ],
              ),
              const SizedBox(height: 6),
              Text(
                _displayText(),
                style: AppTextStyles.bodySmall.copyWith(
                  color: AppColors.textPrimary,
                  height: 1.4,
                ),
              ),
              const SizedBox(height: 6),
              Text(
                _relativeTime(),
                style: AppTextStyles.caption,
              ),
              if (isPending) ...[
                const SizedBox(height: 10),
                Row(
                  children: [
                    Expanded(
                      child: _QuickActionButton(
                        label: 'Подтвердить ✅',
                        color: const Color(0xFF22C55E),
                        onTap: () => voiceCtrl.confirm(linkedAction!.id),
                      ),
                    ),
                    const SizedBox(width: 8),
                    Expanded(
                      child: _QuickActionButton(
                        label: 'Пропустить',
                        color: AppColors.textTertiary,
                        onTap: () => voiceCtrl.skip(linkedAction!.id),
                      ),
                    ),
                  ],
                ),
              ],
            ],
          ),
        ),
      ),
    );
  }

  String _eventLabel() {
    switch (message.proactiveEventType) {
      case 'MorningBriefing':
        return 'УТРЕННИЙ БРИФИНГ';
      case 'MedicationReminder':
        return 'НАПОМИНАНИЕ О ЛЕКАРСТВЕ';
      case 'WeatherAlert':
        return 'ПОГОДНОЕ ОПОВЕЩЕНИЕ';
      case 'AQIAlert':
        return 'КАЧЕСТВО ВОЗДУХА';
      case 'LeaveHomeChecklist':
        return 'ЧЕК-ЛИСТ ВЫХОДА';
      case 'ProactiveRecommendation':
        return 'РЕКОМЕНДАЦИЯ AI';
      default:
        return 'AI СООБЩЕНИЕ';
    }
  }
}

class _QuickActionButton extends StatelessWidget {
  const _QuickActionButton({
    required this.label,
    required this.color,
    required this.onTap,
  });

  final String label;
  final Color color;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return GestureDetector(
      onTap: onTap,
      child: Container(
        height: 32,
        decoration: BoxDecoration(
          color: color.withValues(alpha: 0.10),
          borderRadius: BorderRadius.circular(8),
          border: Border.all(color: color.withValues(alpha: 0.25)),
        ),
        alignment: Alignment.center,
        child: Text(
          label,
          style: TextStyle(
            color: color,
            fontSize: 12,
            fontWeight: FontWeight.w600,
          ),
        ),
      ),
    );
  }
}

class _TypingBubble extends StatelessWidget {
  const _TypingBubble();

  @override
  Widget build(BuildContext context) {
    return Align(
      alignment: Alignment.centerLeft,
      child: Container(
        padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 12),
        decoration: BoxDecoration(
          color: AppColors.surface,
          borderRadius: const BorderRadius.only(
            topLeft: Radius.circular(16),
            topRight: Radius.circular(16),
            bottomLeft: Radius.circular(4),
            bottomRight: Radius.circular(16),
          ),
        ),
        child: const SizedBox(width: 18, height: 12, child: _Dots()),
      ),
    );
  }
}

class _Dots extends StatefulWidget {
  const _Dots();

  @override
  State<_Dots> createState() => _DotsState();
}

class _DotsState extends State<_Dots> with SingleTickerProviderStateMixin {
  late final AnimationController _ac = AnimationController(
    vsync: this,
    duration: const Duration(milliseconds: 900),
  )..repeat();

  @override
  void dispose() {
    _ac.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return AnimatedBuilder(
      animation: _ac,
      builder: (_, _) {
        return Row(
          mainAxisAlignment: MainAxisAlignment.spaceBetween,
          children: List.generate(3, (i) {
            final offset = (i * 0.2);
            final t = ((_ac.value + offset) % 1.0);
            final scale = 0.6 + 0.4 * (1 - (t * 2 - 1).abs()).clamp(0.0, 1.0);
            return Transform.scale(
              scale: scale,
              child: Container(
                width: 5,
                height: 5,
                decoration: const BoxDecoration(
                  color: AppColors.textTertiary,
                  shape: BoxShape.circle,
                ),
              ),
            );
          }),
        );
      },
    );
  }
}

class _ErrorBanner extends StatelessWidget {
  const _ErrorBanner({required this.message});
  final String message;

  @override
  Widget build(BuildContext context) {
    return Container(
      width: double.infinity,
      margin: const EdgeInsets.fromLTRB(16, 0, 16, 6),
      padding: const EdgeInsets.fromLTRB(12, 8, 12, 8),
      decoration: BoxDecoration(
        color: AppColors.redSoft,
        borderRadius: BorderRadius.circular(10),
      ),
      child: Text(
        message,
        style: AppTextStyles.caption.copyWith(color: AppColors.red),
      ),
    );
  }
}

class _ChatEmpty extends StatelessWidget {
  const _ChatEmpty({required this.onPick});
  final Future<void> Function(String text) onPick;

  @override
  Widget build(BuildContext context) {
    final strings = AppStrings(context.watch<AppSettingsController>().language);
    return SingleChildScrollView(
      padding: const EdgeInsets.fromLTRB(24, 24, 24, 24),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.center,
        children: [
          Container(
            width: 72,
            height: 72,
            decoration: BoxDecoration(
              shape: BoxShape.circle,
              gradient: const RadialGradient(
                colors: [
                  Color(0xFF5B8AFF),
                  AppColors.primary,
                  Color(0xFF1D4ED8),
                ],
                stops: [0.0, 0.6, 1.0],
              ),
              boxShadow: [
                BoxShadow(
                  color: AppColors.primary.withValues(alpha: 0.35),
                  blurRadius: 24,
                  spreadRadius: 1,
                ),
              ],
            ),
            child:
                const Icon(Icons.auto_awesome, color: Colors.white, size: 30),
          ),
          const SizedBox(height: 16),
          Text(
            strings.chatEmptyTitle,
            style: AppTextStyles.titleLarge.copyWith(fontSize: 20),
          ),
          const SizedBox(height: 6),
          Text(
            strings.chatEmptyHint,
            textAlign: TextAlign.center,
            style: AppTextStyles.bodySmall.copyWith(
              color: AppColors.textTertiary,
              height: 1.45,
            ),
          ),
          const SizedBox(height: 22),
          Wrap(
            alignment: WrapAlignment.center,
            spacing: 8,
            runSpacing: 8,
            children: strings.chatSuggestions
                .map((s) => _SuggestionChip(label: s, onTap: () => onPick(s)))
                .toList(),
          ),
        ],
      ),
    );
  }
}

class _SuggestionChip extends StatelessWidget {
  const _SuggestionChip({required this.label, required this.onTap});
  final String label;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return GestureDetector(
      onTap: onTap,
      child: Container(
        padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 8),
        decoration: BoxDecoration(
          color: AppColors.blueSoft,
          borderRadius: BorderRadius.circular(20),
          border: Border.all(color: AppColors.primary.withValues(alpha: 0.2)),
        ),
        child: Row(
          mainAxisSize: MainAxisSize.min,
          children: [
            const Icon(Icons.bolt, size: 13, color: AppColors.primary),
            const SizedBox(width: 4),
            Text(
              label,
              style: AppTextStyles.labelMedium.copyWith(
                color: AppColors.blueInk,
                fontSize: 12.5,
                fontWeight: FontWeight.w600,
              ),
            ),
          ],
        ),
      ),
    );
  }
}

class _Composer extends StatelessWidget {
  const _Composer({
    required this.controller,
    required this.enabled,
    required this.onSend,
  });

  final TextEditingController controller;
  final bool enabled;
  final VoidCallback onSend;

  @override
  Widget build(BuildContext context) {
    final strings = AppStrings(context.watch<AppSettingsController>().language);
    return SafeArea(
      top: false,
      child: Padding(
        padding: const EdgeInsets.fromLTRB(16, 8, 16, 16),
        child: Container(
          decoration: BoxDecoration(
            color: AppColors.card,
            borderRadius: BorderRadius.circular(26),
            border: Border.all(color: AppColors.line),
            boxShadow: [
              BoxShadow(
                color: Colors.black.withValues(alpha: 0.06),
                blurRadius: 16,
                offset: const Offset(0, -2),
              ),
            ],
          ),
          padding: const EdgeInsets.fromLTRB(16, 4, 4, 4),
          child: Row(
            children: [
              Expanded(
                child: TextField(
                  controller: controller,
                  enabled: enabled,
                  minLines: 1,
                  maxLines: 4,
                  textInputAction: TextInputAction.send,
                  onSubmitted: (_) => onSend(),
                  style: AppTextStyles.bodyMedium,
                  decoration: InputDecoration(
                    hintText: enabled
                        ? strings.messagePlaceholder
                        : strings.settingUpChat,
                    hintStyle: AppTextStyles.bodyMedium.copyWith(
                      color: AppColors.textQuaternary,
                    ),
                    border: InputBorder.none,
                    isDense: true,
                    contentPadding: const EdgeInsets.symmetric(vertical: 12),
                  ),
                ),
              ),
              GestureDetector(
                onTap: enabled ? onSend : null,
                child: Container(
                  width: 40,
                  height: 40,
                  margin: const EdgeInsets.all(2),
                  decoration: BoxDecoration(
                    color: enabled ? AppColors.primary : AppColors.lineStrong,
                    borderRadius: BorderRadius.circular(20),
                  ),
                  child: const Icon(
                    Icons.arrow_upward,
                    size: 20,
                    color: Colors.white,
                  ),
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}
