@tool
extends PandoraEntity
class_name PandoraQuest

func get_quest() -> BaseQuestResource:
	return get_resource("Quest Resource") as BaseQuestResource

# Helper methods to call QuestSystem methods with the Quest Instance

func start(args: Dictionary = {}) -> BaseQuestResource:
	return QuestSystem.start_quest(_id, args)


func complete(args: Dictionary = {}) -> BaseQuestResource:
	return QuestSystem.complete_quest(_id, args)
	

func update(args: Dictionary = {}) -> BaseQuestResource:
	return QuestSystem.update_quest(_id, args)


func call_method(method: StringName, args: Dictionary = {}) -> BaseQuestResource:
	var quest: BaseQuestResource = get_quest()
	if quest == null:
		push_error("Quest resource is null, cannot call method.")
		return null
	quest.callv(method, [args])
	return quest


func serialize() -> Dictionary:
	var quest: Quest = get_quest()
	assert(quest != null)
	return quest.serialize()
