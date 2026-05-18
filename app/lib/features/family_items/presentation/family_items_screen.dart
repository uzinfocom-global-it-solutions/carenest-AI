import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import 'package:app/features/family_items/application/family_items_controller.dart';

class FamilyItemsScreen extends StatefulWidget {
  const FamilyItemsScreen({super.key});

  @override
  State<FamilyItemsScreen> createState() => _FamilyItemsScreenState();
}

class _FamilyItemsScreenState extends State<FamilyItemsScreen> {
  static const _bg = Color(0xFF0F172A);
  static const _card = Color(0xFF1E293B);
  static const _primary = Color(0xFF4F46E5);
  static const _textPrimary = Color(0xFFE2E8F0);
  static const _textSecondary = Color(0xFF94A3B8);
  static const _border = Color(0xFF2D3748);

  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addPostFrameCallback((_) {
      context.read<FamilyItemsController>().load();
    });
  }

  Future<void> _showAddDialog(BuildContext context) async {
    final ctrl = context.read<FamilyItemsController>();
    final nameCtrl = TextEditingController();
    final notesCtrl = TextEditingController();
    String selectedCategory = 'General';
    bool isRequired = false;

    const categories = [
      'General',
      'Medical',
      'School',
      'Sport',
      'Food',
      'Clothing',
      'Documents',
    ];

    await showDialog<void>(
      context: context,
      builder: (ctx) => StatefulBuilder(
        builder: (ctx, setDialogState) => AlertDialog(
          backgroundColor: _card,
          shape: RoundedRectangleBorder(
            borderRadius: BorderRadius.circular(16),
            side: const BorderSide(color: _border),
          ),
          title: const Text(
            'Добавить предмет',
            style: TextStyle(
              color: _textPrimary,
              fontWeight: FontWeight.w700,
            ),
          ),
          content: SingleChildScrollView(
            child: Column(
              mainAxisSize: MainAxisSize.min,
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                _DialogField(
                  controller: nameCtrl,
                  label: 'Название',
                  hint: 'Например: Ингалятор',
                ),
                const SizedBox(height: 12),
                const Text(
                  'Категория',
                  style: TextStyle(
                    color: _textSecondary,
                    fontSize: 12,
                    fontWeight: FontWeight.w500,
                  ),
                ),
                const SizedBox(height: 6),
                Container(
                  padding: const EdgeInsets.symmetric(horizontal: 12),
                  decoration: BoxDecoration(
                    color: const Color(0xFF0F172A),
                    borderRadius: BorderRadius.circular(10),
                    border: Border.all(color: _border),
                  ),
                  child: DropdownButtonHideUnderline(
                    child: DropdownButton<String>(
                      value: selectedCategory,
                      dropdownColor: _card,
                      isExpanded: true,
                      style: const TextStyle(color: _textPrimary, fontSize: 14),
                      items: categories
                          .map((c) => DropdownMenuItem(
                                value: c,
                                child: Text(_categoryLabel(c)),
                              ))
                          .toList(),
                      onChanged: (v) =>
                          setDialogState(() => selectedCategory = v ?? selectedCategory),
                    ),
                  ),
                ),
                const SizedBox(height: 12),
                _DialogField(
                  controller: notesCtrl,
                  label: 'Заметки (необязательно)',
                  hint: 'Дополнительная информация',
                ),
                const SizedBox(height: 12),
                Row(
                  children: [
                    Switch(
                      value: isRequired,
                      activeThumbColor: _primary,
                      activeTrackColor: _primary.withValues(alpha: 0.4),
                      onChanged: (v) => setDialogState(() => isRequired = v),
                    ),
                    const SizedBox(width: 8),
                    const Text(
                      'Обязательный предмет',
                      style: TextStyle(color: _textPrimary, fontSize: 14),
                    ),
                  ],
                ),
              ],
            ),
          ),
          actions: [
            TextButton(
              onPressed: () => Navigator.of(ctx).pop(),
              child: const Text(
                'Отмена',
                style: TextStyle(color: _textSecondary),
              ),
            ),
            ElevatedButton(
              style: ElevatedButton.styleFrom(
                backgroundColor: _primary,
                foregroundColor: Colors.white,
                shape: RoundedRectangleBorder(
                  borderRadius: BorderRadius.circular(10),
                ),
              ),
              onPressed: () async {
                final name = nameCtrl.text.trim();
                if (name.isEmpty) return;
                Navigator.of(ctx).pop();
                try {
                  await ctrl.create(
                    name: name,
                    category: selectedCategory,
                    isRequired: isRequired,
                    notes: notesCtrl.text.trim().isEmpty
                        ? null
                        : notesCtrl.text.trim(),
                  );
                } catch (e) {
                  if (context.mounted) {
                    ScaffoldMessenger.of(context).showSnackBar(
                      SnackBar(content: Text('Ошибка: $e')),
                    );
                  }
                }
              },
              child: const Text('Добавить'),
            ),
          ],
        ),
      ),
    );
  }

  String _categoryLabel(String category) {
    switch (category) {
      case 'Medical':
        return 'Медицина';
      case 'School':
        return 'Школа';
      case 'Sport':
        return 'Спорт';
      case 'Food':
        return 'Питание';
      case 'Clothing':
        return 'Одежда';
      case 'Documents':
        return 'Документы';
      default:
        return 'Общее';
    }
  }

  IconData _categoryIcon(String category) {
    switch (category) {
      case 'Medical':
        return Icons.medication_outlined;
      case 'School':
        return Icons.school_outlined;
      case 'Sport':
        return Icons.sports_outlined;
      case 'Food':
        return Icons.restaurant_outlined;
      case 'Clothing':
        return Icons.checkroom_outlined;
      case 'Documents':
        return Icons.description_outlined;
      default:
        return Icons.inventory_2_outlined;
    }
  }

  @override
  Widget build(BuildContext context) {
    final ctrl = context.watch<FamilyItemsController>();
    final grouped = ctrl.byCategory;
    final categories = grouped.keys.toList()..sort();

    return Scaffold(
      backgroundColor: _bg,
      appBar: AppBar(
        backgroundColor: _bg,
        elevation: 0,
        centerTitle: false,
        title: const Text(
          'Семейные предметы',
          style: TextStyle(
            color: _textPrimary,
            fontSize: 20,
            fontWeight: FontWeight.w700,
            letterSpacing: -0.3,
          ),
        ),
        iconTheme: const IconThemeData(color: _textPrimary),
        actions: [
          IconButton(
            icon: const Icon(Icons.add, color: _textPrimary),
            onPressed: () => _showAddDialog(context),
            tooltip: 'Добавить предмет',
          ),
        ],
      ),
      body: ctrl.loading && ctrl.items.isEmpty
          ? const Center(
              child: CircularProgressIndicator(
                color: _primary,
                strokeWidth: 2,
              ),
            )
          : ctrl.items.isEmpty
              ? _EmptyState(onAdd: () => _showAddDialog(context))
              : RefreshIndicator(
                  color: _primary,
                  backgroundColor: _card,
                  onRefresh: ctrl.load,
                  child: ListView.builder(
                    padding: const EdgeInsets.fromLTRB(16, 8, 16, 32),
                    itemCount: categories.length,
                    itemBuilder: (context, ci) {
                      final category = categories[ci];
                      final items = grouped[category]!;
                      return Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          Padding(
                            padding: const EdgeInsets.fromLTRB(0, 16, 0, 8),
                            child: Row(
                              children: [
                                Icon(
                                  _categoryIcon(category),
                                  size: 16,
                                  color: _primary,
                                ),
                                const SizedBox(width: 6),
                                Text(
                                  _categoryLabel(category).toUpperCase(),
                                  style: const TextStyle(
                                    color: _primary,
                                    fontSize: 11,
                                    fontWeight: FontWeight.w600,
                                    letterSpacing: 0.8,
                                  ),
                                ),
                                const SizedBox(width: 6),
                                Text(
                                  '(${items.length})',
                                  style: const TextStyle(
                                    color: _textSecondary,
                                    fontSize: 11,
                                  ),
                                ),
                              ],
                            ),
                          ),
                          ...items.map(
                            (item) => _FamilyItemTile(
                              item: item,
                              onToggle: () => ctrl.toggleActive(item.id),
                              onDelete: () async {
                                final confirmed = await _confirmDelete(context, item.name);
                                if (confirmed) {
                                  try {
                                    await ctrl.delete(item.id);
                                  } catch (e) {
                                    if (context.mounted) {
                                      ScaffoldMessenger.of(context).showSnackBar(
                                        SnackBar(content: Text('Ошибка удаления: $e')),
                                      );
                                    }
                                  }
                                }
                              },
                            ),
                          ),
                        ],
                      );
                    },
                  ),
                ),
      floatingActionButton: ctrl.items.isNotEmpty
          ? FloatingActionButton(
              backgroundColor: _primary,
              foregroundColor: Colors.white,
              onPressed: () => _showAddDialog(context),
              child: const Icon(Icons.add),
            )
          : null,
    );
  }

  Future<bool> _confirmDelete(BuildContext context, String name) async {
    return await showDialog<bool>(
          context: context,
          builder: (ctx) => AlertDialog(
            backgroundColor: _card,
            shape: RoundedRectangleBorder(
              borderRadius: BorderRadius.circular(16),
              side: const BorderSide(color: _border),
            ),
            title: const Text(
              'Удалить предмет?',
              style: TextStyle(color: _textPrimary, fontWeight: FontWeight.w700),
            ),
            content: Text(
              '"$name" будет удалён без возможности восстановления.',
              style: const TextStyle(color: _textSecondary),
            ),
            actions: [
              TextButton(
                onPressed: () => Navigator.of(ctx).pop(false),
                child: const Text('Отмена', style: TextStyle(color: _textSecondary)),
              ),
              ElevatedButton(
                style: ElevatedButton.styleFrom(
                  backgroundColor: const Color(0xFFEF4444),
                  foregroundColor: Colors.white,
                  shape: RoundedRectangleBorder(
                    borderRadius: BorderRadius.circular(10),
                  ),
                ),
                onPressed: () => Navigator.of(ctx).pop(true),
                child: const Text('Удалить'),
              ),
            ],
          ),
        ) ??
        false;
  }
}

class _FamilyItemTile extends StatelessWidget {
  const _FamilyItemTile({
    required this.item,
    required this.onToggle,
    required this.onDelete,
  });

  final FamilyItemModel item;
  final VoidCallback onToggle;
  final VoidCallback onDelete;

  static const _card = Color(0xFF1E293B);
  static const _primary = Color(0xFF4F46E5);
  static const _textPrimary = Color(0xFFE2E8F0);
  static const _textSecondary = Color(0xFF94A3B8);
  static const _border = Color(0xFF2D3748);

  @override
  Widget build(BuildContext context) {
    return Dismissible(
      key: ValueKey(item.id),
      direction: DismissDirection.endToStart,
      background: Container(
        margin: const EdgeInsets.only(bottom: 8),
        decoration: BoxDecoration(
          color: const Color(0xFFEF4444).withValues(alpha: 0.15),
          borderRadius: BorderRadius.circular(12),
        ),
        alignment: Alignment.centerRight,
        padding: const EdgeInsets.only(right: 20),
        child: const Icon(Icons.delete_outline, color: Color(0xFFEF4444)),
      ),
      confirmDismiss: (_) async {
        onDelete();
        return false; // deletion handled by controller; don't auto-remove from list
      },
      child: Container(
        margin: const EdgeInsets.only(bottom: 8),
        decoration: BoxDecoration(
          color: _card,
          borderRadius: BorderRadius.circular(12),
          border: Border.all(color: _border),
        ),
        child: ListTile(
          contentPadding: const EdgeInsets.fromLTRB(14, 4, 8, 4),
          leading: Container(
            width: 36,
            height: 36,
            decoration: BoxDecoration(
              color: item.isActive
                  ? _primary.withValues(alpha: 0.15)
                  : const Color(0xFF334155),
              borderRadius: BorderRadius.circular(10),
            ),
            child: Icon(
              item.isRequired ? Icons.star_outlined : Icons.check_box_outline_blank,
              size: 18,
              color: item.isActive ? _primary : _textSecondary,
            ),
          ),
          title: Text(
            item.name,
            style: TextStyle(
              color: item.isActive ? _textPrimary : _textSecondary,
              fontSize: 14,
              fontWeight: FontWeight.w500,
              decoration: item.isActive ? null : TextDecoration.lineThrough,
            ),
          ),
          subtitle: item.notes != null && item.notes!.isNotEmpty
              ? Text(
                  item.notes!,
                  maxLines: 1,
                  overflow: TextOverflow.ellipsis,
                  style: const TextStyle(color: _textSecondary, fontSize: 12),
                )
              : null,
          trailing: Switch(
            value: item.isActive,
            activeThumbColor: _primary,
            activeTrackColor: _primary.withValues(alpha: 0.4),
            onChanged: (_) => onToggle(),
          ),
        ),
      ),
    );
  }
}

class _DialogField extends StatelessWidget {
  const _DialogField({
    required this.controller,
    required this.label,
    required this.hint,
  });

  final TextEditingController controller;
  final String label;
  final String hint;

  static const _textPrimary = Color(0xFFE2E8F0);
  static const _textSecondary = Color(0xFF94A3B8);
  static const _border = Color(0xFF2D3748);

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text(
          label,
          style: const TextStyle(
            color: _textSecondary,
            fontSize: 12,
            fontWeight: FontWeight.w500,
          ),
        ),
        const SizedBox(height: 6),
        TextField(
          controller: controller,
          style: const TextStyle(color: _textPrimary, fontSize: 14),
          decoration: InputDecoration(
            hintText: hint,
            hintStyle: const TextStyle(color: _textSecondary, fontSize: 14),
            filled: true,
            fillColor: const Color(0xFF0F172A),
            contentPadding:
                const EdgeInsets.symmetric(horizontal: 12, vertical: 10),
            border: OutlineInputBorder(
              borderRadius: BorderRadius.circular(10),
              borderSide: const BorderSide(color: _border),
            ),
            enabledBorder: OutlineInputBorder(
              borderRadius: BorderRadius.circular(10),
              borderSide: const BorderSide(color: _border),
            ),
            focusedBorder: OutlineInputBorder(
              borderRadius: BorderRadius.circular(10),
              borderSide: const BorderSide(color: Color(0xFF4F46E5)),
            ),
          ),
        ),
      ],
    );
  }
}

class _EmptyState extends StatelessWidget {
  const _EmptyState({required this.onAdd});
  final VoidCallback onAdd;

  static const _textSecondary = Color(0xFF94A3B8);
  static const _primary = Color(0xFF4F46E5);

  @override
  Widget build(BuildContext context) {
    return Center(
      child: Padding(
        padding: const EdgeInsets.all(32),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            const Icon(
              Icons.inventory_2_outlined,
              size: 52,
              color: _textSecondary,
            ),
            const SizedBox(height: 16),
            const Text(
              'Список пуст',
              style: TextStyle(
                color: _textSecondary,
                fontSize: 16,
                fontWeight: FontWeight.w600,
              ),
            ),
            const SizedBox(height: 8),
            const Text(
              'Добавьте предметы, которые нужно брать с собой',
              textAlign: TextAlign.center,
              style: TextStyle(color: _textSecondary, fontSize: 13),
            ),
            const SizedBox(height: 20),
            ElevatedButton.icon(
              style: ElevatedButton.styleFrom(
                backgroundColor: _primary,
                foregroundColor: Colors.white,
                shape: RoundedRectangleBorder(
                  borderRadius: BorderRadius.circular(12),
                ),
                padding: const EdgeInsets.symmetric(horizontal: 20, vertical: 12),
              ),
              icon: const Icon(Icons.add, size: 18),
              label: const Text('Добавить предмет'),
              onPressed: onAdd,
            ),
          ],
        ),
      ),
    );
  }
}
