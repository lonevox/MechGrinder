extends Control


func _on_play_button_pressed() -> void:
	get_tree().change_scene_to_file("res://World.tscn")


func _on_exit_button_pressed() -> void:
	get_tree().root.propagate_notification(NOTIFICATION_WM_CLOSE_REQUEST)
	get_tree().quit()
