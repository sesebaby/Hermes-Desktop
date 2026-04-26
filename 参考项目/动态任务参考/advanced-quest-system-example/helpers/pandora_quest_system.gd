extends AbstractQuestManagerAPI

signal quest_accepted(quest: BaseQuestResource) # Emitted when a quest gets moved to the ActivePool
signal quest_completed(quest: BaseQuestResource) # Emitted when a quest gets moved to the CompletedPool


const ACTIVE_POOL_UID: StringName = &"uid://jrqtack56fdv"
const COMPLETED_POOL_UID: StringName = &"uid://c5jho6jd6keyw"

var active: ActiveQuestPool = null
var completed: CompletedQuestPool = null


func _ready() -> void:
	add_new_pool(ACTIVE_POOL_UID, "Active")
	add_new_pool(COMPLETED_POOL_UID, "Completed")

	active = get_pool(&"Active") as ActiveQuestPool
	completed = get_pool(&"Completed") as CompletedQuestPool

func is_quest_active(quest_id: String) -> bool:
	assert(quest_id is String)
	var quest_res := get_quest_from_pandora(quest_id)
	return active.is_quest_inside(quest_res)


func is_quest_completed(quest_id: String) -> bool:
	assert(quest_id is String)
	var quest_res := get_quest_from_pandora(quest_id)
	return completed.is_quest_inside(quest_res)


func start_quest(quest_id: String, args: Dictionary = {}) -> BaseQuestResource:
	assert(quest_id is String)
	var pandora_quest: PandoraQuest = Pandora.get_entity(quest_id) as PandoraQuest
	assert(pandora_quest != null)
	
	if !can_start_quest(quest_id):
		return pandora_quest.get_quest()

	var quest_res: BaseQuestResource = pandora_quest.get_quest()

	active.add_quest(quest_res)
	quest_res.start(args)

	quest_accepted.emit(quest_res)

	return quest_res


func complete_quest(quest_id: StringName, args: Dictionary = {}) -> BaseQuestResource:
	var _quest := get_quest_from_pandora(quest_id)

	if !can_complete_quest(quest_id):
		return _quest

	_quest.complete(args)
	move_quest_to_pool(_quest, "Active", "Completed")

	print("Completed quest '%s'." % _quest.quest_name)

	quest_completed.emit(_quest)

	return _quest


func update_quest(_quest_id: StringName, args: Dictionary = {}) -> BaseQuestResource:
	var _quest := get_quest_from_pandora(_quest_id)

	if !active.is_quest_inside(_quest):
		print("Quest '%s' is not in the active pool." % _quest.quest_name)
		return _quest

	_quest.update(args)
	return _quest


func can_start_quest(quest_id: StringName) -> bool:
	var _quest := get_quest_from_pandora(quest_id)

	if completed.is_quest_inside(_quest) || active.is_quest_inside(_quest):
		print("Quest '%s' is either already started or not marked as repeatable." % _quest.quest_name)
		return false


	return true


func can_complete_quest(quest_id: StringName) -> bool:
	var _quest := get_quest_from_pandora(quest_id)

	# -1 == no uncompleted steps
	if _quest.get_first_uncompleted_step() != null:
		print("Quest '%s' has uncompleted steps." % _quest.quest_name)
		return false

	if (!active.is_quest_inside(_quest) or completed.is_quest_inside(_quest)):
		print("Quest '%s' is not in the active pool or is already completed." % _quest.quest_name)
		return false

	return true


func get_active_quests() -> Array[Quest]:
	return active.get_all_quests()


func get_quest_from_pandora(quest_id: String) -> BaseQuestResource:
	assert(quest_id is String)
	var pandora_quest: PandoraQuest = Pandora.get_entity(quest_id) as PandoraQuest
	assert(pandora_quest != null)
	return pandora_quest.get_quest()
