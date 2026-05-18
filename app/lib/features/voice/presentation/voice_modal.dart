import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import 'package:provider/provider.dart';
import 'package:app/shared/theme/app_colors.dart';
import 'package:app/shared/theme/app_text_styles.dart';
import 'package:app/shared/theme/app_spacing.dart';
import 'package:app/shared/widgets/app_avatar.dart';
import 'package:app/features/chats/application/chat_controller.dart';
import 'package:app/features/chats/application/proposal_refresh.dart';
import 'package:app/features/chats/domain/chat_models.dart';
import 'package:app/features/voice/data/voice_service.dart';
import 'package:app/features/settings/application/app_settings_controller.dart';
import 'package:app/core/localization/app_strings.dart';

enum _VoiceStage { idle, listening, thinking, speaking, awaitingConfirm, error }

bool _mentionsAction(String? reply) {
  if (reply == null || reply.isEmpty) return false;
  final t = reply.toLowerCase();
  return t.contains('добавил') ||
      t.contains('добавлен') ||
      t.contains('добавлена') ||
      t.contains('создал') ||
      t.contains('создана') ||
      t.contains('запланировал') ||
      t.contains('запомнил') ||
      t.contains('уже в нашей семье') ||
      t.contains('добавлено') ||
      t.contains('i\'ve added') ||
      t.contains('i\'ve created') ||
      t.contains('added to') ||
      t.contains('is now in') ||
      t.contains('has been added') ||
      t.contains('successfully');
}

const Set<String> _affirmatives = {
  'yes',
  'yeah',
  'yep',
  'yup',
  'sure',
  'ok',
  'okay',
  'confirm',
  'do it',
  'go ahead',
  'please do',
  'sounds good',
  'да',
  'давай',
  'подтверждаю',
  'ha',
  'ha bo‘ladi',
  'ha bo\'ladi',
};

const Set<String> _negatives = {
  'no',
  'nope',
  'cancel',
  'stop',
  'don\'t',
  'do not',
  'нет',
  'не надо',
  'yo‘q',
  'yo\'q',
};

bool _isAffirmative(String text) {
  final t = text.trim().toLowerCase();
  if (t.isEmpty) return false;
  if (_affirmatives.contains(t)) return true;
  for (final word in _affirmatives) {
    if (t.startsWith('$word ') || t.endsWith(' $word') || t == word) return true;
  }
  return false;
}

bool _isNegative(String text) {
  final t = text.trim().toLowerCase();
  if (t.isEmpty) return false;
  if (_negatives.contains(t)) return true;
  for (final word in _negatives) {
    if (t.startsWith('$word ') || t.endsWith(' $word')) return true;
  }
  return false;
}

class VoiceModal extends StatefulWidget {
  const VoiceModal({super.key});

  @override
  State<VoiceModal> createState() => _VoiceModalState();
}

class _VoiceModalState extends State<VoiceModal> {
  _VoiceStage _stage = _VoiceStage.idle;
  String _transcript = '';
  String? _aiReply;
  String? _errorMessage;
  ChatProposal? _pendingProposal;
  int? _pendingProposalMessageId;

  VoiceService get _voice => context.read<VoiceService>();

  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addPostFrameCallback((_) => _startListening());
  }

  Future<void> _startListening() async {
    await _beginListening(showAs: _VoiceStage.listening, clearReply: true);
  }

  Future<void> _listenSilently() async {
    await _beginListening(showAs: null, clearReply: false);
  }

  Future<void> _beginListening({
    required _VoiceStage? showAs,
    required bool clearReply,
  }) async {
    setState(() {
      if (showAs != null) _stage = showAs;
      _transcript = '';
      if (clearReply) {
        _aiReply = null;
        _errorMessage = null;
      }
    });
    final settings = context.read<AppSettingsController>();
    final started = await _voice.startListening(
      sttLocale: settings.sttLocale,
      onResult: (text, isFinal) {
        if (!mounted) return;
        setState(() => _transcript = text);
        if (isFinal && text.trim().isNotEmpty) {
          _handleTranscript(text);
        }
      },
    );
    if (!started && mounted) {
      setState(() {
        _stage = _VoiceStage.error;
        _errorMessage =
            AppStrings(context.read<AppSettingsController>().language).micPermissionDenied;
      });
    }
  }

  Future<void> _stopListening() async {
    final text = await _voice.stopListening();
    if (text.trim().isEmpty) {
      setState(() => _stage = _VoiceStage.idle);
      return;
    }
    await _handleTranscript(text);
  }

  Future<void> _handleTranscript(String text) async {
    if (!mounted) return;

    if (_pendingProposalMessageId != null) {
      if (_isAffirmative(text)) {
        await _confirmPendingProposal();
        return;
      }
      if (_isNegative(text)) {
        _dismissPendingProposal();
        return;
      }
      _pendingProposal = null;
      _pendingProposalMessageId = null;
    }

    setState(() => _stage = _VoiceStage.thinking);

    final chat = context.read<ChatController>();
    final locale = context.read<AppSettingsController>().apiLocale;
    final beforeCount = chat.messages.length;
    await chat.sendMessage(text, inputMode: 'Voice', locale: locale);

    if (!mounted) return;

    final newMessages = chat.messages.skip(beforeCount).toList();
    final aiMsg = newMessages
        .cast<ChatMessageModel?>()
        .lastWhere((m) => m?.senderType == 'Ai', orElse: () => null);

    if (aiMsg == null) {
      setState(() {
        _stage = _VoiceStage.error;
        _errorMessage = chat.error ?? 'No response.';
      });
      return;
    }

    final reply = aiMsg.aiResponse ?? aiMsg.content;
    setState(() {
      _aiReply = reply;
      if (aiMsg.isProposal) {
        _pendingProposal = aiMsg.proposal;
        _pendingProposalMessageId = aiMsg.id;
        _stage = _VoiceStage.speaking;
      } else {
        _pendingProposal = null;
        _pendingProposalMessageId = null;
        _stage = _VoiceStage.speaking;
      }
    });

    await refreshAllAfterAiAction(context);
    if (!mounted) return;

    final ttsLocale = context.read<AppSettingsController>().ttsLocale;
    final spokenText = _pendingProposal == null
        ? reply
        : '${_pendingProposal!.summary}. Should I do that?';
    await _voice.speak(spokenText, ttsLocale: ttsLocale);

    if (!mounted) return;
    if (_pendingProposalMessageId != null) {
      setState(() => _stage = _VoiceStage.awaitingConfirm);
      await _listenSilently();
    }
  }

  Future<void> _confirmPendingProposal() async {
    final id = _pendingProposalMessageId;
    if (id == null) return;
    await _voice.cancel();
    if (!mounted) return;
    setState(() => _stage = _VoiceStage.thinking);

    final chat = context.read<ChatController>();
    final ok = await chat.confirmProposal(id);
    if (!mounted) return;
    if (!ok) {
      setState(() {
        _stage = _VoiceStage.error;
        _errorMessage = chat.error ?? AppStrings(context.read<AppSettingsController>().language).couldNotApply;
      });
      return;
    }
    await refreshAllAfterAiAction(context);
    if (!mounted) return;

    final reply = chat.messages.isEmpty
        ? AppStrings(context.read<AppSettingsController>().language).done
        : (chat.messages.last.aiResponse ?? chat.messages.last.content);
    setState(() {
      _aiReply = reply;
      _pendingProposal = null;
      _pendingProposalMessageId = null;
      _stage = _VoiceStage.speaking;
    });
    final ttsLocale2 = context.read<AppSettingsController>().ttsLocale;
    await _voice.speak(reply, ttsLocale: ttsLocale2);
  }

  Future<void> _dismissPendingProposal() async {
    await _voice.cancel();
    if (!mounted) return;
    final ttsLocale = context.read<AppSettingsController>().ttsLocale;
    final dismissText = AppStrings(context.read<AppSettingsController>().language).cancelling;
    setState(() {
      _pendingProposal = null;
      _pendingProposalMessageId = null;
      _stage = _VoiceStage.speaking;
      _aiReply = dismissText;
    });
    await _voice.speak(dismissText, ttsLocale: ttsLocale);
  }

  Future<void> _close() async {
    await _voice.cancel();
    await _voice.stopSpeaking();
    if (mounted) context.pop();
  }

  @override
  Widget build(BuildContext context) {
    final voice = context.watch<VoiceService>();
    final live = voice.transcript;
    final shownTranscript = (_stage == _VoiceStage.listening ||
            _stage == _VoiceStage.awaitingConfirm)
        ? (live.isEmpty ? _transcript : live)
        : _transcript;

    return Scaffold(
      backgroundColor: AppColors.background,
      body: SafeArea(
        child: Padding(
          padding: const EdgeInsets.fromLTRB(
            AppSpacing.xl,
            AppSpacing.xl,
            AppSpacing.xl,
            AppSpacing.xl,
          ),
          child: Column(
            children: [
              _Header(stage: _stage, onClose: _close),
              const SizedBox(height: 24),
              Expanded(
                child: _BodyForStage(
                  stage: _stage,
                  transcript: shownTranscript,
                  reply: _aiReply,
                  proposal: _pendingProposal,
                  error: _errorMessage,
                ),
              ),
              _FooterForStage(
                stage: _stage,
                hasProposal: _pendingProposalMessageId != null,
                reply: _aiReply,
                onStartListen: _startListening,
                onStopListen: _stopListening,
                onCancel: _close,
                onConfirmProposal: _confirmPendingProposal,
                onDismissProposal: _dismissPendingProposal,
                onSync: () async {
                  await refreshAllAfterAiAction(context);
                  if (mounted) setState(() => _stage = _VoiceStage.idle);
                },
              ),
            ],
          ),
        ),
      ),
    );
  }
}

class _Header extends StatelessWidget {
  const _Header({required this.stage, required this.onClose});
  final _VoiceStage stage;
  final VoidCallback onClose;

  @override
  Widget build(BuildContext context) {
    final strings = AppStrings(context.watch<AppSettingsController>().language);
    final title = switch (stage) {
      _VoiceStage.idle => strings.voiceIdle,
      _VoiceStage.listening => strings.voiceListening,
      _VoiceStage.thinking => strings.voiceThinking,
      _VoiceStage.speaking => strings.voiceSpeaking,
      _VoiceStage.awaitingConfirm => strings.voiceConfirmPrompt,
      _VoiceStage.error => strings.voiceError,
    };
    return Row(
      children: [
        GestureDetector(
          onTap: onClose,
          child: Container(
            width: 36,
            height: 36,
            decoration: BoxDecoration(
              color: AppColors.card,
              shape: BoxShape.circle,
              border: Border.all(color: AppColors.line),
            ),
            child: const Icon(
              Icons.close,
              size: 18,
              color: AppColors.textSecondary,
            ),
          ),
        ),
        Expanded(
          child: Center(child: Text(title, style: AppTextStyles.labelLarge)),
        ),
        GestureDetector(
          onTap: () async {
            onClose();
            await Future<void>.delayed(const Duration(milliseconds: 60));
            if (!context.mounted) return;
            context.go('/chat');
          },
          child: Container(
            width: 36,
            height: 36,
            decoration: BoxDecoration(
              color: AppColors.blueSoft,
              shape: BoxShape.circle,
              border: Border.all(color: AppColors.primary.withValues(alpha: 0.25)),
            ),
            child: const Icon(
              Icons.chat_bubble_outline,
              size: 16,
              color: AppColors.primary,
            ),
          ),
        ),
      ],
    );
  }
}

class _BodyForStage extends StatelessWidget {
  const _BodyForStage({
    required this.stage,
    required this.transcript,
    required this.reply,
    required this.proposal,
    required this.error,
  });

  final _VoiceStage stage;
  final String transcript;
  final String? reply;
  final ChatProposal? proposal;
  final String? error;

  @override
  Widget build(BuildContext context) {
    switch (stage) {
      case _VoiceStage.idle:
        return _IdleBody();
      case _VoiceStage.listening:
        return _ListeningBody(transcript: transcript);
      case _VoiceStage.thinking:
        return _ThinkingBody(transcript: transcript);
      case _VoiceStage.speaking:
        return _SpeakingBody(reply: reply ?? '', proposal: proposal);
      case _VoiceStage.awaitingConfirm:
        return _AwaitingConfirmBody(
          proposal: proposal,
          transcript: transcript,
        );
      case _VoiceStage.error:
        final strings = AppStrings(context.watch<AppSettingsController>().language);
        return _ErrorBody(message: error ?? strings.somethingWentWrong);
    }
  }
}

class _IdleBody extends StatelessWidget {
  @override
  Widget build(BuildContext context) {
    final strings = AppStrings(context.watch<AppSettingsController>().language);
    return Column(
      mainAxisAlignment: MainAxisAlignment.center,
      children: [
        const _BlueOrb(size: 140),
        const SizedBox(height: 24),
        Text(
          strings.tapToTalk,
          style: AppTextStyles.titleLarge.copyWith(fontSize: 22),
        ),
        const SizedBox(height: 8),
        Text(
          strings.voiceHint,
          textAlign: TextAlign.center,
          style: AppTextStyles.bodySmall.copyWith(
            color: AppColors.textTertiary,
          ),
        ),
      ],
    );
  }
}

class _ListeningBody extends StatelessWidget {
  const _ListeningBody({required this.transcript});
  final String transcript;

  @override
  Widget build(BuildContext context) {
    return Column(
      mainAxisAlignment: MainAxisAlignment.center,
      children: [
        const _BlueOrb(size: 120, pulse: true),
        const SizedBox(height: 28),
        Container(
          padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 10),
          decoration: BoxDecoration(
            color: AppColors.blueSoft,
            borderRadius: BorderRadius.circular(14),
          ),
          child: Text(
            transcript.isEmpty ? '…' : transcript,
            textAlign: TextAlign.center,
            style: AppTextStyles.bodyMedium.copyWith(
              color: AppColors.blueInk,
              height: 1.45,
            ),
          ),
        ),
      ],
    );
  }
}

class _ThinkingBody extends StatelessWidget {
  const _ThinkingBody({required this.transcript});
  final String transcript;

  @override
  Widget build(BuildContext context) {
    final strings = AppStrings(context.watch<AppSettingsController>().language);
    return Column(
      mainAxisAlignment: MainAxisAlignment.center,
      children: [
        if (transcript.isNotEmpty)
          Padding(
            padding: const EdgeInsets.only(bottom: 20),
            child: Text(
              transcript,
              textAlign: TextAlign.center,
              style: AppTextStyles.bodySmall.copyWith(
                color: AppColors.textTertiary,
              ),
            ),
          ),
        const AiAvatar(size: 72),
        const SizedBox(height: 16),
        Text(
          strings.voiceThinking,
          style: AppTextStyles.titleLarge.copyWith(fontSize: 18),
        ),
      ],
    );
  }
}

class _SpeakingBody extends StatelessWidget {
  const _SpeakingBody({required this.reply, required this.proposal});
  final String reply;
  final ChatProposal? proposal;

  @override
  Widget build(BuildContext context) {
    final strings = AppStrings(context.watch<AppSettingsController>().language);
    return Column(
      mainAxisAlignment: MainAxisAlignment.center,
      children: [
        const _BlueOrb(size: 130, glow: true),
        const SizedBox(height: 24),
        if (proposal != null) ...[
          Container(
            padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 6),
            decoration: BoxDecoration(
              color: AppColors.blueSoft,
              borderRadius: BorderRadius.circular(14),
            ),
            child: Text(
              strings.proposed,
              style: AppTextStyles.labelSmall.copyWith(
                color: AppColors.primary,
                letterSpacing: 0.6,
              ),
            ),
          ),
          const SizedBox(height: 10),
          Text(
            proposal!.summary,
            textAlign: TextAlign.center,
            style: AppTextStyles.titleLarge.copyWith(
              fontSize: 18,
              fontWeight: FontWeight.w600,
            ),
          ),
        ] else
          Text(
            reply,
            textAlign: TextAlign.center,
            style: AppTextStyles.bodyMedium.copyWith(height: 1.5),
          ),
      ],
    );
  }
}

class _AwaitingConfirmBody extends StatelessWidget {
  const _AwaitingConfirmBody({
    required this.proposal,
    required this.transcript,
  });
  final ChatProposal? proposal;
  final String transcript;

  @override
  Widget build(BuildContext context) {
    final strings = AppStrings(context.watch<AppSettingsController>().language);
    return Column(
      mainAxisAlignment: MainAxisAlignment.center,
      children: [
        const _BlueOrb(size: 110, pulse: true),
        const SizedBox(height: 22),
        if (proposal != null)
          Text(
            proposal!.summary,
            textAlign: TextAlign.center,
            style: AppTextStyles.titleLarge.copyWith(
              fontSize: 18,
              fontWeight: FontWeight.w600,
            ),
          ),
        const SizedBox(height: 14),
        Text(
          transcript.isEmpty
              ? strings.sayYesToApply
              : transcript,
          textAlign: TextAlign.center,
          style: AppTextStyles.bodyMedium.copyWith(
            color: AppColors.textSecondary,
          ),
        ),
      ],
    );
  }
}

class _ErrorBody extends StatelessWidget {
  const _ErrorBody({required this.message});
  final String message;

  @override
  Widget build(BuildContext context) {
    return Column(
      mainAxisAlignment: MainAxisAlignment.center,
      children: [
        const Icon(Icons.error_outline, size: 48, color: AppColors.red),
        const SizedBox(height: 16),
        Text(
          message,
          textAlign: TextAlign.center,
          style: AppTextStyles.bodyMedium.copyWith(color: AppColors.red),
        ),
      ],
    );
  }
}

class _FooterForStage extends StatelessWidget {
  const _FooterForStage({
    required this.stage,
    required this.hasProposal,
    required this.reply,
    required this.onStartListen,
    required this.onStopListen,
    required this.onCancel,
    required this.onConfirmProposal,
    required this.onDismissProposal,
    required this.onSync,
  });

  final _VoiceStage stage;
  final bool hasProposal;
  final String? reply;
  final VoidCallback onStartListen;
  final VoidCallback onStopListen;
  final VoidCallback onCancel;
  final VoidCallback onConfirmProposal;
  final VoidCallback onDismissProposal;
  final VoidCallback onSync;

  @override
  Widget build(BuildContext context) {
    final strings = AppStrings(context.watch<AppSettingsController>().language);
    switch (stage) {
      case _VoiceStage.idle:
      case _VoiceStage.error:
        return _PrimaryButton(
          icon: Icons.mic,
          label: stage == _VoiceStage.error
              ? strings.tryAgain
              : strings.holdToTalk,
          onTap: onStartListen,
        );
      case _VoiceStage.listening:
        return Row(
          mainAxisAlignment: MainAxisAlignment.center,
          children: [
            _CircleButton(icon: Icons.close, filled: false, onTap: onCancel),
            const SizedBox(width: 28),
            _CircleButton(icon: Icons.stop, filled: true, onTap: onStopListen),
          ],
        );
      case _VoiceStage.awaitingConfirm:
        return Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            _AnimatedConfirmButton(
              label: strings.confirm,
              icon: Icons.check_circle_rounded,
              color: AppColors.primary,
              onTap: onConfirmProposal,
            ),
            const SizedBox(height: 10),
            _SecondaryButton(
              icon: Icons.close,
              label: strings.notNow,
              onTap: onDismissProposal,
            ),
            const SizedBox(height: 10),
            Center(
              child: Text(
                strings.orSayYesNo,
                style: AppTextStyles.caption.copyWith(
                  color: AppColors.textTertiary,
                ),
              ),
            ),
          ],
        );
      case _VoiceStage.thinking:
        return _CircleButton(icon: Icons.close, filled: false, onTap: onCancel);
      case _VoiceStage.speaking:
        if (hasProposal) {
          return Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              _AnimatedConfirmButton(
                label: strings.confirm,
                icon: Icons.check_circle_rounded,
                color: AppColors.primary,
                onTap: onConfirmProposal,
              ),
              const SizedBox(height: 10),
              _SecondaryButton(
                icon: Icons.close,
                label: strings.notNow,
                onTap: onDismissProposal,
              ),
            ],
          );
        }
        if (_mentionsAction(reply)) {
          return Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              _AnimatedConfirmButton(
                label: strings.syncData,
                icon: Icons.sync_rounded,
                color: const Color(0xFF16A34A),
                onTap: onSync,
              ),
              const SizedBox(height: 10),
              _SecondaryButton(
                icon: Icons.mic,
                label: strings.replyWithVoice,
                onTap: onStartListen,
              ),
            ],
          );
        }
        return Row(
          children: [
            Expanded(
              child: _SecondaryButton(
                icon: Icons.stop_circle_outlined,
                label: strings.stop,
                onTap: onCancel,
              ),
            ),
            const SizedBox(width: 10),
            Expanded(
              child: _PrimaryButton(
                icon: Icons.mic,
                label: strings.replyWithVoice,
                onTap: onStartListen,
              ),
            ),
          ],
        );
    }
  }
}

class _PrimaryButton extends StatelessWidget {
  const _PrimaryButton({
    required this.icon,
    required this.label,
    required this.onTap,
  });
  final IconData icon;
  final String label;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return GestureDetector(
      onTap: onTap,
      child: Container(
        height: 56,
        decoration: BoxDecoration(
          color: AppColors.primary,
          borderRadius: BorderRadius.circular(AppSpacing.radiusLg),
        ),
        alignment: Alignment.center,
        child: Row(
          mainAxisAlignment: MainAxisAlignment.center,
          children: [
            Icon(icon, size: 18, color: Colors.white),
            const SizedBox(width: 8),
            Text(
              label,
              style: AppTextStyles.labelLarge.copyWith(
                color: Colors.white,
                fontWeight: FontWeight.w600,
              ),
            ),
          ],
        ),
      ),
    );
  }
}

class _SecondaryButton extends StatelessWidget {
  const _SecondaryButton({
    required this.icon,
    required this.label,
    required this.onTap,
  });
  final IconData icon;
  final String label;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return GestureDetector(
      onTap: onTap,
      child: Container(
        height: 56,
        decoration: BoxDecoration(
          color: AppColors.card,
          borderRadius: BorderRadius.circular(AppSpacing.radiusLg),
          border: Border.all(color: AppColors.line),
        ),
        alignment: Alignment.center,
        child: Row(
          mainAxisAlignment: MainAxisAlignment.center,
          children: [
            Icon(icon, size: 18, color: AppColors.textSecondary),
            const SizedBox(width: 8),
            Text(
              label,
              style: AppTextStyles.labelLarge.copyWith(
                color: AppColors.textSecondary,
                fontWeight: FontWeight.w600,
              ),
            ),
          ],
        ),
      ),
    );
  }
}

class _CircleButton extends StatelessWidget {
  const _CircleButton({
    required this.icon,
    required this.filled,
    required this.onTap,
  });
  final IconData icon;
  final bool filled;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return GestureDetector(
      onTap: onTap,
      child: Container(
        width: 64,
        height: 64,
        decoration: BoxDecoration(
          color: filled ? AppColors.red : AppColors.card,
          shape: BoxShape.circle,
          border: filled ? null : Border.all(color: AppColors.line),
        ),
        child: Icon(
          icon,
          size: 28,
          color: filled ? Colors.white : AppColors.textSecondary,
        ),
      ),
    );
  }
}

class _AnimatedConfirmButton extends StatefulWidget {
  const _AnimatedConfirmButton({
    required this.label,
    required this.icon,
    required this.color,
    required this.onTap,
  });
  final String label;
  final IconData icon;
  final Color color;
  final VoidCallback onTap;

  @override
  State<_AnimatedConfirmButton> createState() => _AnimatedConfirmButtonState();
}

class _AnimatedConfirmButtonState extends State<_AnimatedConfirmButton>
    with SingleTickerProviderStateMixin {
  late final AnimationController _ctrl;
  late final Animation<double> _scale;
  late final Animation<Offset> _slide;

  @override
  void initState() {
    super.initState();
    _ctrl = AnimationController(
      vsync: this,
      duration: const Duration(milliseconds: 480),
    );
    _scale = CurvedAnimation(parent: _ctrl, curve: Curves.elasticOut);
    _slide = Tween<Offset>(
      begin: const Offset(0, 0.4),
      end: Offset.zero,
    ).animate(CurvedAnimation(parent: _ctrl, curve: Curves.easeOutCubic));
    _ctrl.forward();
  }

  @override
  void dispose() {
    _ctrl.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return SlideTransition(
      position: _slide,
      child: ScaleTransition(
        scale: _scale,
        child: GestureDetector(
          onTap: widget.onTap,
          child: Container(
            width: double.infinity,
            height: 64,
            decoration: BoxDecoration(
              color: widget.color,
              borderRadius: BorderRadius.circular(20),
              boxShadow: [
                BoxShadow(
                  color: widget.color.withValues(alpha: 0.45),
                  blurRadius: 24,
                  offset: const Offset(0, 8),
                ),
              ],
            ),
            alignment: Alignment.center,
            child: Row(
              mainAxisAlignment: MainAxisAlignment.center,
              children: [
                Icon(widget.icon, size: 24, color: Colors.white),
                const SizedBox(width: 10),
                Text(
                  widget.label,
                  style: const TextStyle(
                    color: Colors.white,
                    fontWeight: FontWeight.w700,
                    fontSize: 17,
                    letterSpacing: 0.2,
                  ),
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }
}

class _BlueOrb extends StatelessWidget {
  const _BlueOrb({required this.size, this.pulse = false, this.glow = false});
  final double size;
  final bool pulse;
  final bool glow;

  @override
  Widget build(BuildContext context) {
    return Container(
      width: size,
      height: size,
      decoration: BoxDecoration(
        shape: BoxShape.circle,
        gradient: const RadialGradient(
          colors: [Color(0xFF5B8AFF), AppColors.primary, Color(0xFF1D4ED8)],
          stops: [0.0, 0.6, 1.0],
        ),
        boxShadow: [
          BoxShadow(
            color: AppColors.primary.withValues(alpha: glow ? 0.55 : 0.35),
            blurRadius: glow ? 60 : 30,
            spreadRadius: glow ? 6 : 0,
          ),
        ],
      ),
      child: pulse
          ? const Center(
              child: Icon(Icons.graphic_eq, size: 36, color: Colors.white),
            )
          : const Center(child: Icon(Icons.mic, size: 32, color: Colors.white)),
    );
  }
}
