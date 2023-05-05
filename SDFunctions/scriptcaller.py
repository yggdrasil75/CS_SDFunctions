import gradio as gr
from modules import ui_components, opts
import clr
sys.path.append(os.path.abspath(os.path.join(os.path.dirname(__file__), '..')))
clr.AddReference('sdfunctions')
from SDFunctions import sdinfin
cs_class = sdinfin()
cs_core = cs_class.core

######################### Script class entrypoint #########################
class Script(scripts.Script):
	BASEDIR = scripts.basedir()
	VALIDATE_REPLACE = True

	def title(self):
		return "Infinite Axis Grid C# Edition"

	def show(self, is_img2img):
		return True

	def ui(self, is_img2img):
		#grid_file = cs_class.ui
		cs_core.listImageFiles()
		cs_class.tryInit()
		gr.HTML(value=f"<br>Confused/new? View <a style=\"border-bottom: 1px #00ffff dotted;\" href=\"{INF_GRID_README}\">the README</a> for usage instructions.<br><br>")
		
		with gr.Row():
			grid_file = gr.Dropdown(value="Create in UI",label="Select grid definition file", choices=["Create in UI"] + core.getNameList())
			def refresh():
				newChoices = ["Create in UI"] + core.getNameList()
				grid_file.choices = newChoices
				return gr.update(choices=newChoices)
			refresh_button = ui_components.ToolButton(value=refresh_symbol, elem_id="infinity_grid_refresh_button")
			refresh_button.click(fn=refresh, inputs=[], outputs=[grid_file])
		output_file_path = gr.Textbox(value="", label="Output folder name (if blank uses yaml filename or current date)")
		page_will_be = gr.HTML(value="(...)<br><br>")
		manualGroup = gr.Group(visible=True)
		manualAxes = list()
		sets = list()
		with manualGroup:
			with gr.Row():
				with gr.Column():
					axisCount = 0
					for group in range(0, 4):
						groupObj = gr.Group(visible=group == 0)
						with groupObj:
							rows = list()
							for i in range(0, 4):
								with gr.Row():
									axisCount += 1
									row_mode = gr.Dropdown(value="", label=f"Axis {axisCount} Mode", choices=[""] + [x.name for x in core.validModes.values()])
									row_value = gr.Textbox(label=f"Axis {axisCount} Value", lines=1)
									fill_row_button = ui_components.ToolButton(value=fill_values_symbol, visible=False)
									def fillAxis(modeName):
										core.clearCaches()
										mode = core.validModes.get(cs_core.cleanName(modeName))
										if mode is None:
											return gr.update()
										elif mode.type == "boolean":
											return "true, false"
										elif mode.valid_list is not None:
											return ", ".join(list(mode.valid_list()))
										raise RuntimeError(f"Can't fill axis for {modeName}")
									fill_row_button.click(fn=fillAxis, inputs=[row_mode], outputs=[row_value])
									def onAxisChange(modeName, outFile):
										mode = core.validModes.get(cs_core.cleanName(modeName))
										buttonUpdate = gr.Button.update(visible=mode is not None and (mode.valid_list is not None or mode.type == "boolean"))
										outFileUpdate = gr.Textbox.update() if outFile != "" else gr.Textbox.update(value=f"autonamed_inf_grid_{datetime.now().strftime('%d_%m_%Y_%H_%M_%S')}")
										return [buttonUpdate, outFileUpdate]
									row_mode.change(fn=onAxisChange, inputs=[row_mode, output_file_path], outputs=[fill_row_button, output_file_path])
									manualAxes += list([row_mode, row_value])
									rows.append(row_mode)
							sets.append([groupObj, rows])
		for group in range(0, 3):
			row_mode = sets[group][1][3]
			groupObj = sets[group + 1][0]
			nextRows = sets[group + 1][1]
			def makeVis(prior, r1, r2, r3, r4):
				return gr.Group.update(visible=prior+r1+r2+r3+r4 != "")
			row_mode.change(fn=makeVis, inputs=[row_mode] + nextRows, outputs=[groupObj])
		gr.HTML('<span style="opacity:0.5;">(More input rows will be automatically added after you select modes above.)</span>')
		grid_file.change(
			fn=lambda x: {"visible": x == "Create in UI", "__type__": "update"},
			inputs=[grid_file],
			outputs=[manualGroup],
			show_progress = False)
		def getPageUrlText(file):
			if file is None:
				return "(...)"
			outPath = opts.outdir_grids or (opts.outdir_img2img_grids if is_img2img else opts.outdir_txt2img_grids)
			fullOutPath = outPath + "/" + file
			return f"Page will be at <a style=\"border-bottom: 1px #00ffff dotted;\" href=\"/file={fullOutPath}/index.html\">(Click me) <code>{fullOutPath}</code></a><br><br>"
		def updatePageUrl(filePath, selectedFile):
			return gr.update(value=getPageUrlText(filePath or (selectedFile.replace(".yml", "") if selectedFile is not None else None)))
		output_file_path.change(fn=updatePageUrl, inputs=[output_file_path, grid_file], outputs=[page_will_be])
		grid_file.change(fn=updatePageUrl, inputs=[output_file_path, grid_file], outputs=[page_will_be])
		with gr.Row():
			do_overwrite = gr.Checkbox(value=False, label="Overwrite existing images (for updating grids)")
			dry_run = gr.Checkbox(value=False, label="Do a dry run to validate your grid file")
			fast_skip = gr.Checkbox(value=False, label="Use more-performant skipping")
		with gr.Row():
			generate_page = gr.Checkbox(value=True, label="Generate infinite-grid webviewer page")
			validate_replace = gr.Checkbox(value=True, label="Validate PromptReplace input")
			publish_gen_metadata = gr.Checkbox(value=True, label="Publish full generation metadata for viewing on-page")
		return [do_overwrite, generate_page, dry_run, validate_replace, publish_gen_metadata, grid_file, fast_skip, output_file_path] + manualAxes
		

	def run(self, p, do_overwrite, generate_page, dry_run, validate_replace, publish_gen_metadata, grid_file, fast_skip, output_file_path, *manualAxes):
		#cs_class.runsimple(p, grid_file, output_file_path)
		cs_class.run(p, do_overwrite, generate_page, dry_run, validate_replace, publish_gen_metadata, grid_file, fast_skip, output_file_path, manualAxes)