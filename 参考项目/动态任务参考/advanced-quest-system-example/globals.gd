extends Node

signal item_delivered(item: Item, to: String)


var inventory: Inventory = null

func _ready() -> void:
	await get_tree().process_frame
	# Here I'm using bitbrain's basic inventory implementation.
	# https://github.com/bitbrain/pandora/blob/godot-4.x/examples/inventory/inventory.gd
	# Normally I would have the inventory in an autoload script as a RefCounted/Resource,
	# But in this implementation, the Inventory is a Node, so I have to manually fetch it
	# from the scene tree.
	inventory = Engine.get_main_loop().get_first_node_in_group("Inventory") as Inventory
