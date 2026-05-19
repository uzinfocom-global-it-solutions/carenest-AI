import 'dart:math' as math;
import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import 'package:provider/provider.dart';
import 'package:app/shared/theme/app_colors.dart';
import 'package:app/shared/theme/app_text_styles.dart';
import 'package:app/shared/widgets/app_avatar.dart';
import 'package:app/features/chats/application/chat_controller.dart';
import 'package:app/features/chats/application/proposal_refresh.dart';
import 'package:app/features/chats/domain/chat_models.dart';
import 'package:app/features/voice/data/voice_service.dart';
import 'package:app/features/settings/application/app_settings_controller.dart';
import 'package:app/core/localization/app_strings.dart';

// ─── constants ────────────────────────────────────────────────────────────────

const _kBg1 = Color(0xFFFFFFFF);
const _kBg2 = Color(0xFFF5F7FF);
const _kOrbLight = Color(0xFF6B9FFF);
const _kOrbDark = Color(0xFF1040C0);
const _kGlass = Color(0x0A2563FF);
const _kGlassBorder = Color(0x182563FF);
const _kTextPrimary = Color(0xFF0A0A0C);
const _kTextSecondary = Color(0xFF3A3A40);
const _kTextTertiary = Color(0xFF6E6E75);

// ─── stage enum ───────────────────────────────────────────────────────────────

enum _VoiceStage { idle, listening, thinking, speaking, awaitingConfirm, error }

// ─── helpers ─────────────────────────────────────────────────────────────────

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
  'yes', 'yeah', 'yep', 'yup', 'sure', 'ok', 'okay', 'confirm',
  'do it', 'go ahead', 'please do', 'sounds good',
  'да', 'давай', 'подтверждаю', 'ha', "ha bo'ladi",
};

const Set<String> _negatives = {
  'no', 'nope', 'cancel', 'stop', "don't", 'do not',
  'нет', 'не надо', "yo'q",
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

// ─── widget ───────────────────────────────────────────────────────────────────

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
  bool _userInterrupted = false;

  VoiceService get _voice => context.read<VoiceService>();

  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addPostFrameCallback((_) => _startListening());
  }

  Future<void> _startListening() async {
    _userInterrupted = true;
    await _voice.stopSpeaking();
    await _voice.cancel();
    _userInterrupted = false;
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

    if (!mounted || _userInterrupted) return;
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
    if (!mounted || _userInterrupted) return;
    await _startListening();
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
    if (!mounted || _userInterrupted) return;
    await _startListening();
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
      backgroundColor: _kBg1,
      body: Container(
        decoration: const BoxDecoration(
          gradient: LinearGradient(
            begin: Alignment.topLeft,
            end: Alignment.bottomRight,
            colors: [_kBg1, _kBg2],
          ),
        ),
        child: SafeArea(
          child: Padding(
            padding: const EdgeInsets.fromLTRB(24, 16, 24, 28),
            child: Column(
              children: [
                _Header(stage: _stage, onClose: _close),
                Expanded(
                  child: AnimatedSwitcher(
                    duration: const Duration(milliseconds: 320),
                    switchInCurve: Curves.easeOutCubic,
                    switchOutCurve: Curves.easeInCubic,
                    transitionBuilder: (child, anim) => FadeTransition(
                      opacity: anim,
                      child: child,
                    ),
                    child: _BodyForStage(
                      key: ValueKey(_stage),
                      stage: _stage,
                      transcript: shownTranscript,
                      reply: _aiReply,
                      proposal: _pendingProposal,
                      error: _errorMessage,
                    ),
                  ),
                ),
                const SizedBox(height: 16),
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
      ),
    );
  }
}

// ─── header ───────────────────────────────────────────────────────────────────

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
        _HeaderIconButton(
          icon: Icons.close,
          onTap: onClose,
        ),
        Expanded(
          child: Center(
            child: AnimatedSwitcher(
              duration: const Duration(milliseconds: 250),
              child: Text(
                title,
                key: ValueKey(title),
                style: AppTextStyles.labelLarge.copyWith(
                  color: _kTextPrimary,
                  fontSize: 15,
                  fontWeight: FontWeight.w600,
                  letterSpacing: 0.2,
                ),
              ),
            ),
          ),
        ),
        _HeaderIconButton(
          icon: Icons.chat_bubble_outline,
          onTap: () async {
            onClose();
            await Future<void>.delayed(const Duration(milliseconds: 60));
            if (!context.mounted) return;
            context.go('/chat');
          },
          highlight: true,
        ),
      ],
    );
  }
}

class _HeaderIconButton extends StatelessWidget {
  const _HeaderIconButton({
    required this.icon,
    required this.onTap,
    this.highlight = false,
  });
  final IconData icon;
  final VoidCallback onTap;
  final bool highlight;

  @override
  Widget build(BuildContext context) {
    return GestureDetector(
      onTap: onTap,
      child: Container(
        width: 38,
        height: 38,
        decoration: BoxDecoration(
          color: highlight
              ? AppColors.primary.withValues(alpha: 0.25)
              : _kGlass,
          shape: BoxShape.circle,
          border: Border.all(
            color: highlight
                ? AppColors.primary.withValues(alpha: 0.5)
                : _kGlassBorder,
          ),
        ),
        child: Icon(
          icon,
          size: 17,
          color: highlight ? AppColors.primary : _kTextSecondary,
        ),
      ),
    );
  }
}

// ─── body ─────────────────────────────────────────────────────────────────────

class _BodyForStage extends StatelessWidget {
  const _BodyForStage({
    super.key,
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
        return const _IdleBody();
      case _VoiceStage.listening:
        return _ListeningBody(transcript: transcript);
      case _VoiceStage.thinking:
        return _ThinkingBody(transcript: transcript);
      case _VoiceStage.speaking:
        return _SpeakingBody(reply: reply ?? '', proposal: proposal);
      case _VoiceStage.awaitingConfirm:
        return _AwaitingConfirmBody(proposal: proposal, transcript: transcript);
      case _VoiceStage.error:
        final strings = AppStrings(context.watch<AppSettingsController>().language);
        return _ErrorBody(message: error ?? strings.somethingWentWrong);
    }
  }
}

// ── idle ──────────────────────────────────────────────────────────────────────

class _IdleBody extends StatelessWidget {
  const _IdleBody();

  @override
  Widget build(BuildContext context) {
    final strings = AppStrings(context.watch<AppSettingsController>().language);
    return Column(
      mainAxisAlignment: MainAxisAlignment.center,
      children: [
        const _VoiceOrb(stage: _VoiceStage.idle),
        const SizedBox(height: 32),
        Text(
          strings.tapToTalk,
          style: AppTextStyles.titleLarge.copyWith(
            color: _kTextPrimary,
            fontSize: 22,
            fontWeight: FontWeight.w700,
          ),
        ),
        const SizedBox(height: 10),
        Padding(
          padding: const EdgeInsets.symmetric(horizontal: 24),
          child: Text(
            strings.voiceHint,
            textAlign: TextAlign.center,
            style: AppTextStyles.bodySmall.copyWith(
              color: _kTextTertiary,
              height: 1.5,
            ),
          ),
        ),
      ],
    );
  }
}

// ── listening ─────────────────────────────────────────────────────────────────

class _ListeningBody extends StatelessWidget {
  const _ListeningBody({required this.transcript});
  final String transcript;

  @override
  Widget build(BuildContext context) {
    return Column(
      mainAxisAlignment: MainAxisAlignment.center,
      children: [
        const _VoiceOrb(stage: _VoiceStage.listening),
        const SizedBox(height: 32),
        if (transcript.isNotEmpty) ...[
          _GlassCard(
            child: Text(
              transcript,
              textAlign: TextAlign.center,
              style: AppTextStyles.bodyMedium.copyWith(
                color: _kTextPrimary,
                height: 1.5,
              ),
            ),
          ),
        ] else
          Text(
            '…',
            style: AppTextStyles.bodyLarge.copyWith(
              color: _kTextTertiary,
              fontSize: 28,
            ),
          ),
      ],
    );
  }
}

// ── thinking ──────────────────────────────────────────────────────────────────

class _ThinkingBody extends StatelessWidget {
  const _ThinkingBody({required this.transcript});
  final String transcript;

  @override
  Widget build(BuildContext context) {
    return Column(
      mainAxisAlignment: MainAxisAlignment.center,
      children: [
        const _ThinkingOrb(),
        const SizedBox(height: 28),
        const _BouncingDots(),
        if (transcript.isNotEmpty) ...[
          const SizedBox(height: 24),
          _GlassCard(
            child: Text(
              transcript,
              textAlign: TextAlign.center,
              style: AppTextStyles.bodySmall.copyWith(
                color: _kTextSecondary,
                height: 1.5,
              ),
            ),
          ),
        ],
      ],
    );
  }
}

// ── speaking ──────────────────────────────────────────────────────────────────

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
        const _VoiceOrb(stage: _VoiceStage.speaking),
        const SizedBox(height: 8),
        const _SoundBars(),
        const SizedBox(height: 24),
        if (proposal != null) ...[
          _GlassPill(label: strings.proposed),
          const SizedBox(height: 12),
          Padding(
            padding: const EdgeInsets.symmetric(horizontal: 8),
            child: Text(
              proposal!.summary,
              textAlign: TextAlign.center,
              style: AppTextStyles.titleLarge.copyWith(
                color: _kTextPrimary,
                fontSize: 18,
                fontWeight: FontWeight.w600,
              ),
            ),
          ),
        ] else
          _GlassCard(
            child: Text(
              reply,
              textAlign: TextAlign.center,
              style: AppTextStyles.bodyMedium.copyWith(
                color: _kTextPrimary,
                height: 1.55,
              ),
            ),
          ),
      ],
    );
  }
}

// ── awaiting confirm ──────────────────────────────────────────────────────────

class _AwaitingConfirmBody extends StatelessWidget {
  const _AwaitingConfirmBody({required this.proposal, required this.transcript});
  final ChatProposal? proposal;
  final String transcript;

  @override
  Widget build(BuildContext context) {
    final strings = AppStrings(context.watch<AppSettingsController>().language);
    return Column(
      mainAxisAlignment: MainAxisAlignment.center,
      children: [
        const _VoiceOrb(stage: _VoiceStage.awaitingConfirm),
        const SizedBox(height: 28),
        if (proposal != null) ...[
          Padding(
            padding: const EdgeInsets.symmetric(horizontal: 8),
            child: Text(
              proposal!.summary,
              textAlign: TextAlign.center,
              style: AppTextStyles.titleLarge.copyWith(
                color: _kTextPrimary,
                fontSize: 18,
                fontWeight: FontWeight.w600,
              ),
            ),
          ),
          const SizedBox(height: 16),
        ],
        _GlassCard(
          child: Text(
            transcript.isEmpty ? strings.sayYesToApply : transcript,
            textAlign: TextAlign.center,
            style: AppTextStyles.bodyMedium.copyWith(
              color: transcript.isEmpty ? _kTextTertiary : _kTextPrimary,
              height: 1.5,
            ),
          ),
        ),
      ],
    );
  }
}

// ── error ─────────────────────────────────────────────────────────────────────

class _ErrorBody extends StatelessWidget {
  const _ErrorBody({required this.message});
  final String message;

  @override
  Widget build(BuildContext context) {
    return Column(
      mainAxisAlignment: MainAxisAlignment.center,
      children: [
        Container(
          width: 80,
          height: 80,
          decoration: BoxDecoration(
            shape: BoxShape.circle,
            color: AppColors.red.withValues(alpha: 0.15),
            border: Border.all(color: AppColors.red.withValues(alpha: 0.4)),
          ),
          child: const Icon(Icons.error_outline, size: 36, color: AppColors.red),
        ),
        const SizedBox(height: 20),
        Padding(
          padding: const EdgeInsets.symmetric(horizontal: 16),
          child: Text(
            message,
            textAlign: TextAlign.center,
            style: AppTextStyles.bodyMedium.copyWith(color: AppColors.red),
          ),
        ),
      ],
    );
  }
}

// ─── orb components ───────────────────────────────────────────────────────────

class _VoiceOrb extends StatefulWidget {
  const _VoiceOrb({required this.stage});
  final _VoiceStage stage;

  @override
  State<_VoiceOrb> createState() => _VoiceOrbState();
}

class _VoiceOrbState extends State<_VoiceOrb> with TickerProviderStateMixin {
  late final AnimationController _pulseCtrl;
  late final AnimationController _rippleCtrl;
  late final Animation<double> _pulseAnim;

  @override
  void initState() {
    super.initState();
    _pulseCtrl = AnimationController(
      vsync: this,
      duration: const Duration(milliseconds: 2000),
    )..repeat(reverse: true);

    _rippleCtrl = AnimationController(
      vsync: this,
      duration: const Duration(milliseconds: 2200),
    )..repeat();

    _pulseAnim = Tween<double>(begin: 1.0, end: 1.10).animate(
      CurvedAnimation(parent: _pulseCtrl, curve: Curves.easeInOut),
    );
  }

  @override
  void dispose() {
    _pulseCtrl.dispose();
    _rippleCtrl.dispose();
    super.dispose();
  }

  bool get _isListening =>
      widget.stage == _VoiceStage.listening ||
      widget.stage == _VoiceStage.awaitingConfirm;
  bool get _isSpeaking => widget.stage == _VoiceStage.speaking;

  @override
  Widget build(BuildContext context) {
    return SizedBox(
      width: 210,
      height: 210,
      child: Stack(
        alignment: Alignment.center,
        children: [
          // ripple rings (listening / awaitingConfirm)
          if (_isListening)
            AnimatedBuilder(
              animation: _rippleCtrl,
              builder: (context, child) => CustomPaint(
                size: const Size(210, 210),
                painter: _RipplePainter(_rippleCtrl.value, AppColors.primary),
              ),
            ),
          // breathing glow (speaking)
          if (_isSpeaking)
            AnimatedBuilder(
              animation: _pulseAnim,
              builder: (_, child) => Transform.scale(
                scale: _pulseAnim.value,
                child: child,
              ),
              child: Container(
                width: 190,
                height: 190,
                decoration: BoxDecoration(
                  shape: BoxShape.circle,
                  gradient: RadialGradient(
                    colors: [
                      AppColors.primary.withValues(alpha: 0.20),
                      AppColors.primary.withValues(alpha: 0.0),
                    ],
                  ),
                ),
              ),
            ),
          // outer soft ring (idle)
          if (widget.stage == _VoiceStage.idle)
            AnimatedBuilder(
              animation: _pulseAnim,
              builder: (context, child) => Container(
                width: 165 + (_pulseAnim.value - 1) * 30,
                height: 165 + (_pulseAnim.value - 1) * 30,
                decoration: BoxDecoration(
                  shape: BoxShape.circle,
                  border: Border.all(
                    color: AppColors.primary.withValues(alpha: 0.15),
                    width: 1,
                  ),
                ),
              ),
            ),
          // main orb
          AnimatedBuilder(
            animation: _pulseAnim,
            builder: (_, child) {
              final scale = _isSpeaking
                  ? _pulseAnim.value
                  : _isListening
                      ? 1.0 + (_pulseAnim.value - 1.0) * 0.4
                      : 1.0;
              return Transform.scale(scale: scale, child: child);
            },
            child: Container(
              width: 148,
              height: 148,
              decoration: BoxDecoration(
                shape: BoxShape.circle,
                gradient: const RadialGradient(
                  colors: [_kOrbLight, AppColors.primary, _kOrbDark],
                  stops: [0.0, 0.55, 1.0],
                ),
                boxShadow: [
                  BoxShadow(
                    color: AppColors.primary.withValues(
                      alpha: _isSpeaking ? 0.75 : 0.5,
                    ),
                    blurRadius: _isSpeaking ? 90 : 60,
                    spreadRadius: _isSpeaking ? 10 : 4,
                  ),
                ],
              ),
              child: Center(child: _OrbIcon(stage: widget.stage)),
            ),
          ),
        ],
      ),
    );
  }
}

class _OrbIcon extends StatelessWidget {
  const _OrbIcon({required this.stage});
  final _VoiceStage stage;

  @override
  Widget build(BuildContext context) {
    final icon = switch (stage) {
      _VoiceStage.listening => Icons.graphic_eq,
      _VoiceStage.awaitingConfirm => Icons.help_outline_rounded,
      _VoiceStage.speaking => Icons.volume_up_rounded,
      _VoiceStage.error => Icons.error_outline,
      _ => Icons.mic,
    };
    return Icon(icon, size: 44, color: Colors.white.withValues(alpha: 0.95));
  }
}

// ─── thinking orb (AI avatar style) ──────────────────────────────────────────

class _ThinkingOrb extends StatefulWidget {
  const _ThinkingOrb();

  @override
  State<_ThinkingOrb> createState() => _ThinkingOrbState();
}

class _ThinkingOrbState extends State<_ThinkingOrb>
    with SingleTickerProviderStateMixin {
  late final AnimationController _ctrl;

  @override
  void initState() {
    super.initState();
    _ctrl = AnimationController(
      vsync: this,
      duration: const Duration(milliseconds: 3000),
    )..repeat();
  }

  @override
  void dispose() {
    _ctrl.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return SizedBox(
      width: 148,
      height: 148,
      child: Stack(
        alignment: Alignment.center,
        children: [
          // rotating arc
          AnimatedBuilder(
            animation: _ctrl,
            builder: (context, child) => Transform.rotate(
              angle: _ctrl.value * 2 * math.pi,
              child: CustomPaint(
                size: const Size(148, 148),
                painter: _ArcPainter(),
              ),
            ),
          ),
          // inner orb
          Container(
            width: 120,
            height: 120,
            decoration: BoxDecoration(
              shape: BoxShape.circle,
              gradient: const RadialGradient(
                colors: [_kOrbLight, AppColors.primary, _kOrbDark],
                stops: [0.0, 0.55, 1.0],
              ),
              boxShadow: [
                BoxShadow(
                  color: AppColors.primary.withValues(alpha: 0.5),
                  blurRadius: 50,
                  spreadRadius: 4,
                ),
              ],
            ),
            child: const Center(
              child: AiAvatar(size: 56),
            ),
          ),
        ],
      ),
    );
  }
}

class _ArcPainter extends CustomPainter {
  @override
  void paint(Canvas canvas, Size size) {
    final rect = Offset.zero & size;
    final paint = Paint()
      ..shader = SweepGradient(
        colors: [
          AppColors.primary.withValues(alpha: 0.0),
          AppColors.primary.withValues(alpha: 0.8),
          AppColors.primary.withValues(alpha: 0.0),
        ],
        stops: const [0.0, 0.5, 1.0],
      ).createShader(rect)
      ..style = PaintingStyle.stroke
      ..strokeWidth = 2.5
      ..strokeCap = StrokeCap.round;
    canvas.drawArc(
      rect.deflate(1),
      0,
      math.pi * 2,
      false,
      paint,
    );
  }

  @override
  bool shouldRepaint(_ArcPainter old) => false;
}

// ─── ripple painter ───────────────────────────────────────────────────────────

class _RipplePainter extends CustomPainter {
  final double progress;
  final Color color;
  const _RipplePainter(this.progress, this.color);

  @override
  void paint(Canvas canvas, Size size) {
    final center = Offset(size.width / 2, size.height / 2);
    final maxRadius = size.width / 2;
    for (int i = 0; i < 3; i++) {
      final t = (progress + i / 3) % 1.0;
      final radius = maxRadius * 0.45 + maxRadius * 0.55 * t;
      final opacity = (1.0 - t) * 0.45;
      canvas.drawCircle(
        center,
        radius,
        Paint()
          ..color = color.withValues(alpha: opacity)
          ..style = PaintingStyle.stroke
          ..strokeWidth = 1.5,
      );
    }
  }

  @override
  @override
  bool shouldRepaint(_RipplePainter old) => old.progress != progress;
}

// ─── bouncing dots ────────────────────────────────────────────────────────────

class _BouncingDots extends StatefulWidget {
  const _BouncingDots();

  @override
  State<_BouncingDots> createState() => _BouncingDotsState();
}

class _BouncingDotsState extends State<_BouncingDots>
    with SingleTickerProviderStateMixin {
  late final AnimationController _ctrl;

  @override
  void initState() {
    super.initState();
    _ctrl = AnimationController(
      vsync: this,
      duration: const Duration(milliseconds: 1100),
    )..repeat();
  }

  @override
  void dispose() {
    _ctrl.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return AnimatedBuilder(
      animation: _ctrl,
      builder: (context, child) {
        return Row(
          mainAxisSize: MainAxisSize.min,
          children: List.generate(3, (i) {
            final delay = i / 3;
            final t = (_ctrl.value + 1.0 - delay) % 1.0;
            final bounce = math.sin(t * math.pi).clamp(0.0, 1.0);
            return Padding(
              padding: const EdgeInsets.symmetric(horizontal: 5),
              child: Transform.translate(
                offset: Offset(0, -10 * bounce),
                child: Container(
                  width: 7,
                  height: 7,
                  decoration: BoxDecoration(
                    shape: BoxShape.circle,
                    color: Colors.white.withValues(alpha: 0.4 + 0.6 * bounce),
                  ),
                ),
              ),
            );
          }),
        );
      },
    );
  }
}

// ─── sound bars ───────────────────────────────────────────────────────────────

class _SoundBars extends StatefulWidget {
  const _SoundBars();

  @override
  State<_SoundBars> createState() => _SoundBarsState();
}

class _SoundBarsState extends State<_SoundBars>
    with SingleTickerProviderStateMixin {
  late final AnimationController _ctrl;
  final _rand = math.Random(42);
  late final List<double> _phases;
  late final List<double> _speeds;

  @override
  void initState() {
    super.initState();
    _phases = List.generate(7, (_) => _rand.nextDouble() * math.pi * 2);
    _speeds = List.generate(7, (_) => 1.5 + _rand.nextDouble() * 2.0);
    _ctrl = AnimationController(
      vsync: this,
      duration: const Duration(milliseconds: 1000),
    )..repeat();
  }

  @override
  void dispose() {
    _ctrl.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return AnimatedBuilder(
      animation: _ctrl,
      builder: (context, child) {
        return Row(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.end,
          children: List.generate(7, (i) {
            final t = _ctrl.value * _speeds[i] * math.pi * 2 + _phases[i];
            final h = 6.0 + 18.0 * ((math.sin(t) + 1) / 2);
            return Padding(
              padding: const EdgeInsets.symmetric(horizontal: 2.5),
              child: AnimatedContainer(
                duration: const Duration(milliseconds: 80),
                width: 4,
                height: h,
                decoration: BoxDecoration(
                  color: Colors.white.withValues(alpha: 0.5 + 0.5 * (h / 24)),
                  borderRadius: BorderRadius.circular(2),
                ),
              ),
            );
          }),
        );
      },
    );
  }
}

// ─── glass components ─────────────────────────────────────────────────────────

class _GlassCard extends StatelessWidget {
  const _GlassCard({required this.child});
  final Widget child;

  @override
  Widget build(BuildContext context) {
    return Container(
      width: double.infinity,
      padding: const EdgeInsets.symmetric(horizontal: 20, vertical: 14),
      decoration: BoxDecoration(
        color: _kGlass,
        borderRadius: BorderRadius.circular(18),
        border: Border.all(color: _kGlassBorder),
      ),
      child: child,
    );
  }
}

class _GlassPill extends StatelessWidget {
  const _GlassPill({required this.label});
  final String label;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 5),
      decoration: BoxDecoration(
        color: AppColors.primary.withValues(alpha: 0.2),
        borderRadius: BorderRadius.circular(20),
        border: Border.all(color: AppColors.primary.withValues(alpha: 0.4)),
      ),
      child: Text(
        label,
        style: AppTextStyles.labelSmall.copyWith(
          color: const Color(0xFF90BBFF),
          letterSpacing: 0.8,
          fontWeight: FontWeight.w700,
        ),
      ),
    );
  }
}

// ─── footer ───────────────────────────────────────────────────────────────────

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
          icon: Icons.mic_rounded,
          label: stage == _VoiceStage.error ? strings.tryAgain : strings.holdToTalk,
          onTap: onStartListen,
        );

      case _VoiceStage.listening:
        return Row(
          mainAxisAlignment: MainAxisAlignment.center,
          children: [
            _CircleButton(icon: Icons.close, filled: false, onTap: onCancel),
            const SizedBox(width: 32),
            _CircleButton(icon: Icons.stop_rounded, filled: true, onTap: onStopListen),
          ],
        );

      case _VoiceStage.thinking:
        return _CircleButton(icon: Icons.close, filled: false, onTap: onCancel);

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
            Text(
              strings.orSayYesNo,
              style: AppTextStyles.caption.copyWith(color: _kTextTertiary),
            ),
          ],
        );

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
              Row(
                children: [
                  Expanded(
                    child: _SecondaryButton(
                      icon: Icons.close,
                      label: strings.notNow,
                      onTap: onDismissProposal,
                    ),
                  ),
                  const SizedBox(width: 10),
                  Expanded(
                    child: _SecondaryButton(
                      icon: Icons.mic_rounded,
                      label: strings.replyWithVoice,
                      onTap: onStartListen,
                    ),
                  ),
                ],
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
                icon: Icons.mic_rounded,
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
                icon: Icons.mic_rounded,
                label: strings.replyWithVoice,
                onTap: onStartListen,
              ),
            ),
          ],
        );
    }
  }
}

// ─── buttons ──────────────────────────────────────────────────────────────────

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
        height: 58,
        decoration: BoxDecoration(
          gradient: const LinearGradient(
            colors: [Color(0xFF4080FF), AppColors.primary, Color(0xFF1A50D8)],
            stops: [0.0, 0.5, 1.0],
          ),
          borderRadius: BorderRadius.circular(18),
          boxShadow: [
            BoxShadow(
              color: AppColors.primary.withValues(alpha: 0.45),
              blurRadius: 24,
              offset: const Offset(0, 8),
            ),
          ],
        ),
        alignment: Alignment.center,
        child: Row(
          mainAxisAlignment: MainAxisAlignment.center,
          children: [
            Icon(icon, size: 20, color: Colors.white),
            const SizedBox(width: 9),
            Text(
              label,
              style: const TextStyle(
                color: Colors.white,
                fontWeight: FontWeight.w700,
                fontSize: 16,
                letterSpacing: 0.2,
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
        height: 54,
        decoration: BoxDecoration(
          color: _kGlass,
          borderRadius: BorderRadius.circular(16),
          border: Border.all(color: _kGlassBorder),
        ),
        alignment: Alignment.center,
        child: Row(
          mainAxisAlignment: MainAxisAlignment.center,
          children: [
            Icon(icon, size: 18, color: _kTextSecondary),
            const SizedBox(width: 7),
            Text(
              label,
              style: const TextStyle(
                color: _kTextSecondary,
                fontWeight: FontWeight.w600,
                fontSize: 14,
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
        width: 66,
        height: 66,
        decoration: BoxDecoration(
          color: filled ? AppColors.red : _kGlass,
          shape: BoxShape.circle,
          border: Border.all(
            color: filled
                ? AppColors.red.withValues(alpha: 0.6)
                : _kGlassBorder,
          ),
          boxShadow: filled
              ? [
                  BoxShadow(
                    color: AppColors.red.withValues(alpha: 0.4),
                    blurRadius: 20,
                    offset: const Offset(0, 6),
                  ),
                ]
              : null,
        ),
        child: Icon(
          icon,
          size: 28,
          color: filled ? Colors.white : _kTextSecondary,
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
      duration: const Duration(milliseconds: 500),
    );
    _scale = CurvedAnimation(parent: _ctrl, curve: Curves.elasticOut);
    _slide = Tween<Offset>(
      begin: const Offset(0, 0.35),
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
            height: 62,
            decoration: BoxDecoration(
              color: widget.color,
              borderRadius: BorderRadius.circular(20),
              boxShadow: [
                BoxShadow(
                  color: widget.color.withValues(alpha: 0.50),
                  blurRadius: 28,
                  offset: const Offset(0, 10),
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
