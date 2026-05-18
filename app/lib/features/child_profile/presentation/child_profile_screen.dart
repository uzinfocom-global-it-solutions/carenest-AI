import 'dart:io';
import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import 'package:provider/provider.dart';
import 'package:image_picker/image_picker.dart';
import 'package:shared_preferences/shared_preferences.dart';
import 'package:app/shared/theme/app_colors.dart';
import 'package:app/shared/theme/app_text_styles.dart';
import 'package:app/shared/theme/app_spacing.dart';
import 'package:app/shared/widgets/app_avatar.dart';
import 'package:app/features/children/application/children_controller.dart';
import 'package:app/features/children/domain/child_models.dart';
import 'package:app/core/localization/app_strings.dart';
import 'package:app/features/settings/application/app_settings_controller.dart';

class ChildProfileScreen extends StatefulWidget {
  const ChildProfileScreen({super.key, required this.childId});
  final String childId;

  @override
  State<ChildProfileScreen> createState() => _ChildProfileScreenState();
}

class _ChildProfileScreenState extends State<ChildProfileScreen> {
  String? _avatarPath;

  @override
  void initState() {
    super.initState();
    _loadAvatar();
    WidgetsBinding.instance.addPostFrameCallback((_) {
      final id = int.tryParse(widget.childId);
      if (id != null) {
        context.read<ChildrenController>().loadNotes(id);
      }
    });
  }

  Future<void> _loadAvatar() async {
    final prefs = await SharedPreferences.getInstance();
    if (mounted) {
      setState(() {
        _avatarPath = prefs.getString('avatar_${widget.childId}');
      });
    }
  }

  Future<void> _pickAvatar() async {
    final picker = ImagePicker();
    final image = await picker.pickImage(source: ImageSource.gallery);
    if (image != null) {
      final prefs = await SharedPreferences.getInstance();
      await prefs.setString('avatar_${widget.childId}', image.path);
      if (mounted) {
        setState(() {
          _avatarPath = image.path;
        });
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    final strings = AppStrings(context.watch<AppSettingsController>().language);
    final childrenCtrl = context.watch<ChildrenController>();
    final id = int.tryParse(widget.childId);
    final child = id == null
        ? null
        : childrenCtrl.children.cast<ChildModel?>().firstWhere(
            (c) => c?.id == id, orElse: () => null);
    final index = child == null ? 0 : childrenCtrl.children.indexOf(child);
    final routines = child == null
        ? <ChildRoutineModel>[]
        : childrenCtrl.routines.where((r) => r.childId == child.id).toList();
    return Scaffold(
      backgroundColor: AppColors.background,
      body: SafeArea(
        child: child == null
            ? Center(child: Text(strings.childNotFound))
            : Column(
                children: [
                  _buildHeader(context, child, index, strings),
                  Expanded(
                    child: ListView(
                      padding: const EdgeInsets.fromLTRB(18, 8, 18, 32),
                      children: [
                        _EditCard(child: child, index: index),
                        const SizedBox(height: 20),
                        _HealthNotesCard(
                          childId: id!,
                          notes: childrenCtrl.notesFor(id),
                        ),
                        const SizedBox(height: 20),
                        _RoutinesCard(routines: routines, strings: strings),
                        const SizedBox(height: 20),
                        _VoiceCard(
                          childName: child.displayName,
                          onTap: () => context.push('/voice'),
                          onChat: () => context.go('/chat'),
                        ),
                      ],
                    ),
                  ),
                ],
              ),
      ),
    );
  }

  Widget _buildHeader(BuildContext context, ChildModel child, int index, AppStrings strings) {
    return Padding(
      padding: const EdgeInsets.fromLTRB(16, 16, 16, 0),
      child: Row(
        children: [
          GestureDetector(
            onTap: () => context.canPop() ? context.pop() : context.go('/family'),
            child: Container(
              width: 36, height: 36,
              decoration: BoxDecoration(
                color: AppColors.card,
                shape: BoxShape.circle,
                border: Border.all(color: AppColors.line),
              ),
              child: const Icon(Icons.arrow_back_ios_new, size: 16, color: AppColors.textSecondary),
            ),
          ),
          const SizedBox(width: 12),
          GestureDetector(
            onTap: _pickAvatar,
            child: Stack(
              children: [
                if (_avatarPath != null && File(_avatarPath!).existsSync())
                  CircleAvatar(
                    radius: 20,
                    backgroundImage: FileImage(File(_avatarPath!)),
                  )
                else
                  AppAvatar(
                    initial: child.displayName.isEmpty ? '?' : child.displayName[0],
                    size: 40,
                    childColor: ChildAvatarColor.forIndex(index),
                  ),
                Positioned(
                  bottom: 0,
                  right: 0,
                  child: Container(
                    padding: const EdgeInsets.all(3),
                    decoration: BoxDecoration(
                      color: AppColors.primary,
                      shape: BoxShape.circle,
                      border: Border.all(color: AppColors.background, width: 1.5),
                    ),
                    child: const Icon(Icons.camera_alt, size: 9, color: Colors.white),
                  ),
                ),
              ],
            ),
          ),
          const SizedBox(width: 10),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(child.displayName, style: AppTextStyles.titleMedium),
                Text(child.ageLabel, style: AppTextStyles.caption.copyWith(color: AppColors.textTertiary)),
              ],
            ),
          ),
        ],
      ),
    );
  }
}

class _EditCard extends StatefulWidget {
  const _EditCard({required this.child, required this.index});
  final ChildModel child;
  final int index;

  @override
  State<_EditCard> createState() => _EditCardState();
}

class _EditCardState extends State<_EditCard> {
  late final TextEditingController _nameCtrl;
  late int _ageYears;
  bool _editing = false;
  bool _saving = false;
  String? _error;

  @override
  void initState() {
    super.initState();
    _nameCtrl = TextEditingController(text: widget.child.displayName);
    _ageYears = widget.child.ageYears;
  }

  @override
  void dispose() {
    _nameCtrl.dispose();
    super.dispose();
  }

  Future<void> _save() async {
    final name = _nameCtrl.text.trim();
    if (name.isEmpty) { setState(() => _error = 'Name is required'); return; }
    setState(() { _saving = true; _error = null; });
    final ctrl = context.read<ChildrenController>();
    final updated = await ctrl.updateChild(
      widget.child.id, displayName: name, ageYears: _ageYears);
    if (!mounted) return;
    if (updated != null) {
      setState(() { _editing = false; _saving = false; });
    } else {
      setState(() { _saving = false; _error = ctrl.error ?? 'Could not save.'; });
    }
  }

  @override
  Widget build(BuildContext context) {
    final isRu = context.read<AppSettingsController>().language == AppLanguage.russian;
    return Container(
      padding: const EdgeInsets.all(16),
      decoration: BoxDecoration(
        color: AppColors.card,
        borderRadius: BorderRadius.circular(AppSpacing.radiusLg),
        border: Border.all(color: AppColors.line),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              Text(isRu ? 'Профиль' : 'Profile',
                  style: AppTextStyles.labelLarge.copyWith(fontSize: 15)),
              const Spacer(),
              GestureDetector(
                onTap: () => setState(() => _editing = !_editing),
                child: Container(
                  padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 6),
                  decoration: BoxDecoration(
                    color: _editing ? AppColors.surface : AppColors.blueSoft,
                    borderRadius: BorderRadius.circular(20),
                  ),
                  child: Text(
                    _editing ? (isRu ? 'Отмена' : 'Cancel') : (isRu ? 'Изменить' : 'Edit'),
                    style: AppTextStyles.labelSmall.copyWith(
                      color: _editing ? AppColors.textSecondary : AppColors.primary,
                      fontSize: 12,
                    ),
                  ),
                ),
              ),
            ],
          ),
          const SizedBox(height: 14),
          if (!_editing) ...[
            _InfoRow(label: isRu ? 'Имя' : 'Name', value: widget.child.displayName),
            const SizedBox(height: 8),
            _InfoRow(label: isRu ? 'Возраст' : 'Age', value: '${widget.child.ageYears} ${isRu ? "лет" : "years"} · ${widget.child.ageGroup.toLowerCase()}'),
          ] else ...[
            TextField(
              controller: _nameCtrl,
              style: AppTextStyles.bodyMedium,
              decoration: InputDecoration(
                labelText: isRu ? 'Имя' : 'Name',
                filled: true, fillColor: AppColors.surface,
                border: OutlineInputBorder(borderRadius: BorderRadius.circular(10), borderSide: BorderSide.none),
                contentPadding: const EdgeInsets.symmetric(horizontal: 14, vertical: 12),
              ),
            ),
            const SizedBox(height: 10),
            Row(
              children: [
                Text(isRu ? 'Возраст: ' : 'Age: ', style: AppTextStyles.bodySmall),
                IconButton(
                  onPressed: _ageYears > 0 ? () => setState(() => _ageYears--) : null,
                  icon: const Icon(Icons.remove_circle_outline, size: 20),
                  color: AppColors.primary,
                ),
                Text('$_ageYears', style: AppTextStyles.titleMedium),
                IconButton(
                  onPressed: _ageYears < 25 ? () => setState(() => _ageYears++) : null,
                  icon: const Icon(Icons.add_circle_outline, size: 20),
                  color: AppColors.primary,
                ),
              ],
            ),
            if (_error != null) ...[
              const SizedBox(height: 6),
              Text(_error!, style: const TextStyle(color: AppColors.red, fontSize: 12)),
            ],
            const SizedBox(height: 10),
            SizedBox(
              width: double.infinity,
              child: GestureDetector(
                onTap: _saving ? null : _save,
                child: Container(
                  height: 44,
                  decoration: BoxDecoration(
                    color: _saving ? AppColors.lineStrong : AppColors.primary,
                    borderRadius: BorderRadius.circular(12),
                  ),
                  alignment: Alignment.center,
                  child: _saving
                      ? const SizedBox(width: 18, height: 18, child: CircularProgressIndicator(strokeWidth: 2, color: Colors.white))
                      : Text(isRu ? 'Сохранить' : 'Save',
                          style: const TextStyle(color: Colors.white, fontWeight: FontWeight.w600)),
                ),
              ),
            ),
          ],
        ],
      ),
    );
  }
}

class _InfoRow extends StatelessWidget {
  const _InfoRow({required this.label, required this.value});
  final String label;
  final String value;

  @override
  Widget build(BuildContext context) {
    return Row(
      children: [
        Text(label, style: AppTextStyles.caption.copyWith(color: AppColors.textTertiary)),
        const SizedBox(width: 8),
        Expanded(child: Text(value, style: AppTextStyles.bodyMedium, overflow: TextOverflow.ellipsis)),
      ],
    );
  }
}


class _RoutinesCard extends StatelessWidget {
  const _RoutinesCard({required this.routines, required this.strings});
  final List<ChildRoutineModel> routines;
  final AppStrings strings;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.all(16),
      decoration: BoxDecoration(
        color: AppColors.card,
        borderRadius: BorderRadius.circular(AppSpacing.radiusLg),
        border: Border.all(color: AppColors.line),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              Text(strings.routines, style: AppTextStyles.labelLarge.copyWith(fontSize: 15)),
              const Spacer(),
              Text('${routines.length}', style: AppTextStyles.caption.copyWith(color: AppColors.textTertiary)),
            ],
          ),
          const SizedBox(height: 10),
          if (routines.isEmpty)
            Text(strings.routinesHint,
                style: AppTextStyles.bodySmall.copyWith(color: AppColors.textTertiary))
          else
            ...routines.map((r) => Padding(
              padding: const EdgeInsets.only(bottom: 8),
              child: Row(
                children: [
                  Container(
                    width: 8, height: 8,
                    decoration: const BoxDecoration(color: AppColors.avatarGreen, shape: BoxShape.circle),
                  ),
                  const SizedBox(width: 10),
                  Expanded(child: Text('${r.title} · ${r.timeLabel}',
                      style: AppTextStyles.bodySmall)),
                  Text(r.repeatPattern.toLowerCase(),
                      style: AppTextStyles.caption.copyWith(color: AppColors.textTertiary)),
                ],
              ),
            )),
        ],
      ),
    );
  }
}

class _HealthNotesCard extends StatefulWidget {
  const _HealthNotesCard({required this.childId, required this.notes});
  final int childId;
  final List<ChildNoteModel> notes;

  @override
  State<_HealthNotesCard> createState() => _HealthNotesCardState();
}

class _HealthNotesCardState extends State<_HealthNotesCard> {
  bool _adding = false;
  bool _saving = false;
  final _noteCtrl = TextEditingController();
  static const _selectedType = 'Health';

  @override
  void dispose() {
    _noteCtrl.dispose();
    super.dispose();
  }

  Future<void> _save() async {
    final text = _noteCtrl.text.trim();
    if (text.isEmpty) return;
    setState(() => _saving = true);
    final ok = await context
        .read<ChildrenController>()
        .addNote(widget.childId, noteType: _selectedType, note: text);
    if (!mounted) return;
    if (ok) {
      _noteCtrl.clear();
      setState(() { _adding = false; _saving = false; });
    } else {
      setState(() => _saving = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final isRu = context.read<AppSettingsController>().language == AppLanguage.russian;


    final healthNotes = widget.notes.where(
      (n) => n.noteType == 'Health' || n.noteType == 'Allergy',
    ).toList();
    final otherNotes = widget.notes.where(
      (n) => n.noteType != 'Health' && n.noteType != 'Allergy',
    ).toList();

    return Container(
      padding: const EdgeInsets.all(16),
      decoration: BoxDecoration(
        color: AppColors.card,
        borderRadius: BorderRadius.circular(AppSpacing.radiusLg),
        border: Border.all(color: AppColors.line),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              const Icon(Icons.medical_information_outlined,
                  size: 18, color: AppColors.red),
              const SizedBox(width: 8),
              Expanded(
                child: Text(
                  isRu ? 'Здоровье и состояния' : 'Health & Conditions',
                  style: AppTextStyles.labelLarge.copyWith(fontSize: 15),
                ),
              ),
              GestureDetector(
                onTap: () => setState(() => _adding = !_adding),
                child: Container(
                  padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 6),
                  decoration: BoxDecoration(
                    color: _adding ? AppColors.surface : AppColors.redSoft,
                    borderRadius: BorderRadius.circular(20),
                  ),
                  child: Text(
                    _adding
                        ? (isRu ? 'Отмена' : 'Cancel')
                        : (isRu ? '+ Добавить' : '+ Add'),
                    style: AppTextStyles.labelSmall.copyWith(
                      color: _adding ? AppColors.textSecondary : AppColors.red,
                      fontSize: 12,
                    ),
                  ),
                ),
              ),
            ],
          ),

          if (_adding) ...[
            const SizedBox(height: 14),
            TextField(
              controller: _noteCtrl,
              maxLines: 3,
              minLines: 2,
              autofocus: true,
              style: AppTextStyles.bodySmall,
              decoration: InputDecoration(
                hintText: isRu
                    ? 'Опишите состояние, аллергию, особенность…'
                    : 'Describe the condition, allergy, note…',
                hintStyle: AppTextStyles.bodySmall.copyWith(color: AppColors.textTertiary),
                filled: true,
                fillColor: AppColors.surface,
                border: OutlineInputBorder(
                  borderRadius: BorderRadius.circular(10),
                  borderSide: BorderSide.none,
                ),
                contentPadding: const EdgeInsets.symmetric(horizontal: 14, vertical: 10),
              ),
            ),
            const SizedBox(height: 10),
            SizedBox(
              width: double.infinity,
              child: GestureDetector(
                onTap: _saving ? null : _save,
                child: Container(
                  height: 42,
                  decoration: BoxDecoration(
                    color: _saving ? AppColors.lineStrong : AppColors.red,
                    borderRadius: BorderRadius.circular(10),
                  ),
                  alignment: Alignment.center,
                  child: _saving
                      ? const SizedBox(width: 18, height: 18,
                          child: CircularProgressIndicator(strokeWidth: 2, color: Colors.white))
                      : Text(isRu ? 'Сохранить' : 'Save',
                          style: const TextStyle(color: Colors.white, fontWeight: FontWeight.w600)),
                ),
              ),
            ),
          ],

          if (healthNotes.isNotEmpty) ...[
            const SizedBox(height: 14),
            Text(isRu ? 'БОЛЕЗНИ / АЛЛЕРГИИ' : 'CONDITIONS / ALLERGIES',
                style: AppTextStyles.labelSmall.copyWith(
                  color: AppColors.textTertiary, fontSize: 10, letterSpacing: 0.8)),
            const SizedBox(height: 8),
            ...healthNotes.map((n) => _NoteRow(note: n)),
          ],

          if (otherNotes.isNotEmpty) ...[
            const SizedBox(height: 14),
            Text(isRu ? 'ЗАМЕТКИ' : 'NOTES',
                style: AppTextStyles.labelSmall.copyWith(
                  color: AppColors.textTertiary, fontSize: 10, letterSpacing: 0.8)),
            const SizedBox(height: 8),
            ...otherNotes.take(5).map((n) => _NoteRow(note: n)),
          ],

          if (widget.notes.isEmpty && !_adding)
            Padding(
              padding: const EdgeInsets.only(top: 10),
              child: Text(
                isRu
                    ? 'Нет записей. Нажмите «+ Добавить» или скажите ИИ о здоровье ребёнка.'
                    : 'No records. Tap «+ Add» or tell the AI about the child\'s health.',
                style: AppTextStyles.bodySmall.copyWith(color: AppColors.textTertiary, height: 1.4),
              ),
            ),
        ],
      ),
    );
  }
}

class _NoteRow extends StatelessWidget {
  const _NoteRow({required this.note});
  final ChildNoteModel note;

  static const _typeColors = <String, Color>{
    'Health':      AppColors.red,
    'Allergy':     Color(0xFF7A5800),
    'Hydration':   AppColors.blue2,
    'Observation': AppColors.textSecondary,
    'Preference':  AppColors.avatarViolet,
    'Reminder':    AppColors.primary,
  };

  static const _typeBgs = <String, Color>{
    'Health':      AppColors.redSoft,
    'Allergy':     AppColors.amberSoft,
    'Hydration':   AppColors.blueSoft,
    'Observation': AppColors.surface,
    'Preference':  AppColors.violetSoft,
    'Reminder':    AppColors.blueSoft,
  };

  @override
  Widget build(BuildContext context) {
    final color = _typeColors[note.noteType] ?? AppColors.textSecondary;
    final bg    = _typeBgs[note.noteType]    ?? AppColors.surface;
    return Padding(
      padding: const EdgeInsets.only(bottom: 8),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Container(
            padding: const EdgeInsets.symmetric(horizontal: 7, vertical: 3),
            decoration: BoxDecoration(color: bg, borderRadius: BorderRadius.circular(6)),
            child: Text(note.noteType,
                style: TextStyle(fontSize: 10, fontWeight: FontWeight.w700, color: color)),
          ),
          const SizedBox(width: 8),
          Expanded(
            child: Text(note.note,
                style: AppTextStyles.bodySmall.copyWith(height: 1.4)),
          ),
        ],
      ),
    );
  }
}

class _VoiceCard extends StatelessWidget {
  const _VoiceCard({required this.childName, required this.onTap, required this.onChat});
  final String childName;
  final VoidCallback onTap;
  final VoidCallback onChat;

  @override
  Widget build(BuildContext context) {
    final isRu = context.read<AppSettingsController>().language == AppLanguage.russian;
    return Container(
      padding: const EdgeInsets.all(16),
      decoration: BoxDecoration(
        gradient: const LinearGradient(
          begin: Alignment.topLeft,
          end: Alignment.bottomRight,
          colors: [Color(0xFFEFF3FF), Color(0xFFE3EAFF)],
        ),
        borderRadius: BorderRadius.circular(AppSpacing.radiusLg),
        border: Border.all(color: AppColors.primary.withValues(alpha: 0.18)),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(
            isRu ? 'Расскажи CareNestAI о $childName' : 'Tell CareNestAI about $childName',
            style: AppTextStyles.labelLarge.copyWith(fontSize: 14.5),
          ),
          const SizedBox(height: 4),
          Text(
            isRu
                ? 'Голосом или текстом добавь заметки, режим, чувствительности — ИИ запомнит и учтёт.'
                : 'Via voice or text — add notes, routines, sensitivities. AI will remember and personalize.',
            style: AppTextStyles.caption.copyWith(color: AppColors.textSecondary, height: 1.4),
          ),
          const SizedBox(height: 12),
          Row(
            children: [
              Expanded(
                child: GestureDetector(
                  onTap: onTap,
                  child: Container(
                    height: 44,
                    decoration: BoxDecoration(
                      gradient: const RadialGradient(
                        colors: [Color(0xFF5B8AFF), AppColors.primary, Color(0xFF1D4ED8)],
                        stops: [0.0, 0.6, 1.0],
                      ),
                      borderRadius: BorderRadius.circular(12),
                    ),
                    child: Row(
                      mainAxisAlignment: MainAxisAlignment.center,
                      children: [
                        const Icon(Icons.mic, color: Colors.white, size: 16),
                        const SizedBox(width: 6),
                        Text(isRu ? 'Голос' : 'Voice',
                            style: const TextStyle(color: Colors.white, fontWeight: FontWeight.w600)),
                      ],
                    ),
                  ),
                ),
              ),
              const SizedBox(width: 10),
              Expanded(
                child: GestureDetector(
                  onTap: onChat,
                  child: Container(
                    height: 44,
                    decoration: BoxDecoration(
                      color: AppColors.card,
                      borderRadius: BorderRadius.circular(12),
                      border: Border.all(color: AppColors.primary.withValues(alpha: 0.3)),
                    ),
                    child: Row(
                      mainAxisAlignment: MainAxisAlignment.center,
                      children: [
                        const Icon(Icons.chat_bubble_outline, color: AppColors.primary, size: 16),
                        const SizedBox(width: 6),
                        Text(isRu ? 'Чат' : 'Chat',
                            style: const TextStyle(color: AppColors.primary, fontWeight: FontWeight.w600)),
                      ],
                    ),
                  ),
                ),
              ),
            ],
          ),
        ],
      ),
    );
  }
}
