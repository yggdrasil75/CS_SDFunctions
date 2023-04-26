using Microsoft.SqlServer.Server;
using Python.Runtime;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Configuration;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Policy;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Linq;
using YamlDotNet.Serialization;
using static System.Net.WebRequestMethods;

namespace SDFunctions
{
	public class sdinfin
	{
		public string title;
		public bool show;
		internal string basedir;
		internal bool validatereplace;
		List<object> imagecache;
		bool hasInited;
		public GridGenCore core = new();

		public Dictionary<string, GridSettingMode> validModes = new Dictionary<string, GridSettingMode>();

		public void registerMode(string name, GridSettingMode mode)
		{
			mode.name = name;

			validModes[GridGenCore.CleanName(name)] = mode;
		}

		public sdinfin()
		{
			title = "Generate Infinite-Axis Grid";
			show = true;
			registerMode("Model", new GridSettingMode(false, "text", applyModel, null, null, core.models, core.cleanModel));

		}

		private void applyModel(object arg1, object arg2)
		{
			using (Py.GIL())
			{
				dynamic shared = Py.Import("modules.shared");
				dynamic opts = shared.opts;
				dynamic sd_models = Py.Import("modules.sd_models");
				opts.sd_model_checkpoint = core.GetModelFor(arg2 as string);
				sd_models.reload_model_weights();
			}
		}
		private void applyVae(object arg1, object arg2)
		{
			using (Py.GIL())
			{
				dynamic shared = Py.Import("modules.shared");
				dynamic opts = shared.opts;
				dynamic sd_vae = Py.Import("modules.sd_vae");
				string vaeName = GridGenCore.CleanName(arg2 as string);
				if (vaeName == "none" || vaeName == "None") vaeName = "None";
				else vaeName = core.getVaeFor(vaeName);
				opts.sd_vae = vaeName;
				sd_vae.reload_model_weights(null);
			}
		}
		private void applyClipSkip(object arg1, object arg2)
		{
			using (Py.GIL())
			{
				dynamic shared = Py.Import("modules.shared");
				dynamic opts = shared.opts;
				opts.CLIP_stop_at_last_layers = arg2 as int?;
			}
		}
		private void applyRestoreFaces(dynamic p, dynamic v)
		{
			using (Py.GIL())
			{
				dynamic shared = Py.Import("modules.shared");
				string input = v.ToString().ToLower().Trim();
				if (input == "false")
				{
					p.restore_faces = false;
					return;
				}
				p.restore_faces = true;
				dynamic restorer = core.GetBestInList(input, core.facerestorers);
				if (restorer != null)
				{
					shared.opts.face_restoration_model = restorer;
				}
			}
		}
		private void applycodeformerweight(object arg1, object arg2)
		{
			using (Py.GIL())
			{
				dynamic shared = Py.Import("modules.shared");
				shared.opts.code_former_weight = arg2 as float?;
			}
		}
		private void applyensd(object arg1, object arg2)
		{
			using (Py.GIL())
			{
				dynamic shared = Py.Import("modules.shared");
				shared.opts.eta_noise_seed_delta = arg2 as int?;
			}
		}

		private void applyPromptReplace(object arg1, object arg2)
		{
			string prompt = (arg1 as dynamic).prompt;
			if (prompt != null)
			{
				(arg1 as dynamic).prompt = pr(prompt, arg2 as string);
			}
		}
		private void applyNegPromptReplace(object arg1, object arg2)
		{
			string prompt = (arg1 as dynamic).negative_prompt;
			if (prompt != null)
			{
				(arg1 as dynamic).negative_prompt = pr(prompt, arg2 as string);
			}
		}
		private string pr(string InputPrompt, string PRValue)
		{
			var val = PRValue.Split('|');
			if (val.Length != 2) throw new Exception("invalid prompt replace. need a single =");
			string match = val[0].Trim();
			string replace = val[1].Trim();
			if (validatereplace && !InputPrompt.Contains(match)) throw new Exception("Match not in prompt");
			return InputPrompt.Replace(match, replace);
		}

		private void applyEnableHr(dynamic arg1, object arg2)
		{
			arg1.enable_Hr = arg2;
			if (arg2 != null && arg1.denoising_strenght == null)
			{
				arg1.denoising_strength = 0.75;
			}
		}

		public void tryinit()
		{

			if (hasInited) return;
			hasInited = true;
			using (Py.GIL())
			{
				dynamic sd_models = Py.Import("modules.sd_models");
				//registerMode("Model", GridSettingMode(dry = False, type = "text", apply = applyModel, clean = cleanModel, valid_list = lambda: list(map(lambda m: m.title, sd_models.checkpoints_list.values()))))
				registerMode("Model", new GridSettingMode(false, "text", applyModel, null, null, core.models, core.cleanModel));
				//registerMode("VAE", GridSettingMode(dry = False, type = "text", apply = applyVae, clean = cleanVae, valid_list = lambda: list(sd_vae.vae_dict.keys()) + ['none', 'auto', 'automatic']))
				registerMode("vae", new GridSettingMode(false, "text", applyVae, null, null, core.vaelist, core.cleanVae));
				//registerMode("Sampler", GridSettingMode(dry = True, type = "text", apply = applyField("sampler_name"), valid_list = lambda: list(sd_samplers.all_samplers_map.keys())))
				registerMode("sampler", new(true, "text", "sampler_name", null, null, core.sd_samplers));
				//registerMode("ClipSkip", GridSettingMode(dry = False, type = "integer", min = 1, max = 12, apply = applyClipSkip))
				registerMode("ClipSkip", new GridSettingMode(false, "integer", applyClipSkip, 1, 12));
				//registerMode("Restore Faces", GridSettingMode(dry = True, type = "text", apply = applyRestoreFaces, valid_list = lambda: list(map(lambda m: m.name(), shared.face_restorers)) + ["true", "false"]))
				registerMode("restore faces", new GridSettingMode(true, "text", applyRestoreFaces, null, null, core.facerestorers));
				//registerMode("CodeFormer Weight", GridSettingMode(dry = True, type = "decimal", min = 0, max = 1, apply = applyCodeformerWeight))
				registerMode("codeformer weight", new GridSettingMode(true, "decimal", applycodeformerweight, 0, 1));
				//registerMode("ETA Noise Seed Delta", GridSettingMode(dry = True, type = "integer", apply = applyEnsd))
				registerMode("ETA Noise Seed Delta", new GridSettingMode(true, "integer", applyensd));
				//registerMode("Enable HighRes Fix", GridSettingMode(dry = True, type = "boolean", apply = applyEnableHr))
				registerMode("enablehighresfix", new GridSettingMode(true, "boolean", applyEnableHr));
				//registerMode("Prompt Replace", GridSettingMode(dry = True, type = "text", apply = applyPromptReplace))
				registerMode("prompt replace", new GridSettingMode(true, "text", applyPromptReplace));
				//registerMode("Negative Prompt Replace", GridSettingMode(dry = True, type = "text", apply = applyNegPromptReplace))
				registerMode("negative prompt replace", new GridSettingMode(true, "text", applyNegPromptReplace));
				//registerMode("N Prompt Replace", GridSettingMode(dry = True, type = "text", apply = applyNegPromptReplace))
				registerMode("n prompt replace", new GridSettingMode(true, "text", applyNegPromptReplace));
				for (int i = 0; i < 10; i++)
				{
					//registerMode("Prompt Replace", GridSettingMode(dry = True, type = "text", apply = applyPromptReplace))
					registerMode($"prompt replace {i}", new GridSettingMode(true, "text", applyPromptReplace));
					//registerMode("Negative Prompt Replace", GridSettingMode(dry = True, type = "text", apply = applyNegPromptReplace))
					registerMode($"negative prompt replace {i}", new GridSettingMode(true, "text", applyNegPromptReplace));
					//registerMode("N Prompt Replace", GridSettingMode(dry = True, type = "text", apply = applyNegPromptReplace))
					registerMode($"n prompt replace{i}", new GridSettingMode(true, "text", applyNegPromptReplace));
				}

				//modes = ["var seed", "seed", "width", "height", "device priority"]
				//fields = ["subseed", "seed", "width", "height", "deviceidpriority"]
				//for field, mode in enumerate(modes):
				//	registerMode(mode, GridSettingMode(dry = True, type = "integer", apply = applyField(fields[field])))
				//registerMode("HighRes Resize Width", GridSettingMode(dry = True, type = "integer", apply = applyField("hr_resize_x")))
				//registerMode("HighRes Resize Height", GridSettingMode(dry = True, type = "integer", apply = applyField("hr_resize_y")))
				//registerMode("HighRes Upscale to Width", GridSettingMode(dry = True, type = "integer", apply = applyField("hr_upscale_to_x")))
				//registerMode("HighRes Upscale to Height", GridSettingMode(dry = True, type = "integer", apply = applyField("hr_upscale_to_y")))
				foreach (var mode in new Dictionary<string, string> { { "var seed", "subseed" }, { "seed", "seed" }, { "width", "width" }, { "height", "height" }, { "device priority", "deviceidpriority" }, { "HighRes Resize Width", "hr_resize_x" }, { "HighRes Resize Height", "hr_resize_y" }, { "HighRes Upscale to Width", "hr_upscale_to_x" }, { "HighRes Upscale to Height", "hr_upscale_to_y" } })
				{
					registerMode(mode.Key, new GridSettingMode(true, "integer", mode.Value));
				}

				//modes = ["prompt", "negative prompt", "random"]
				//fields = ["prompt", "negative_prompt", "randomtime"]
				//for field, mode in enumerate(modes):
				//	registerMode(mode, GridSettingMode(dry = True, type = "text", apply = applyField(fields[field])))
				foreach (var mode in new Dictionary<string, string> { { "prompt", "prompt" }, { "negative prompt", "negative_prompt" }, { "random", "randomtime" }, { "batch size", "batch_size" } })
				{
					registerMode(mode.Key, new GridSettingMode(true, "text", mode.Value));
				}

				//registerMode("Steps", GridSettingMode(dry = True, type = "integer", min = 0, max = 200, apply = applyField("steps")))
				registerMode("steps", new GridSettingMode(true, "integer", "steps", 0, 200));
				//registerMode("CFG Scale", GridSettingMode(dry = True, type = "decimal", min = 0, max = 500, apply = applyField("cfg_scale")))
				//registerMode("Image CFG Scale", GridSettingMode(dry = True, type = "decimal", min = 0, max = 500, apply = applyField("image_cfg_scale")))
				//registerMode("Use Result Index", GridSettingMode(dry = True, type = "integer", min = 0, max = 500, apply = applyField("inf_grid_use_result_index")))
				foreach (var mode in new Dictionary<string, string> { { "CFG Scale", "cfg_scale" }, { "Image CFG Scale", "image_cfg_scale" }, { "Use Result Index", "inf_grid_use_result_index" } })
				{
					registerMode(mode.Key, new GridSettingMode(true, "decimal", mode.Value, 0, 500));
				}
				//registerMode("Tiling", GridSettingMode(dry = True, type = "boolean", apply = applyField("tiling")))
				registerMode("tiling", new(true, "boolean", "tiling"));

				//registerMode("Var Strength", GridSettingMode(dry = True, type = "decimal", min = 0, max = 1, apply = applyField("subseed_strength")))
				//registerMode("Denoising", GridSettingMode(dry = True, type = "decimal", min = 0, max = 1, apply = applyField("denoising_strength")))
				//registerMode("ETA", GridSettingMode(dry = True, type = "decimal", min = 0, max = 1, apply = applyField("eta")))
				//registerMode("Sigma Churn", GridSettingMode(dry = True, type = "decimal", min = 0, max = 1, apply = applyField("s_churn")))
				//registerMode("Sigma TMin", GridSettingMode(dry = True, type = "decimal", min = 0, max = 1, apply = applyField("s_tmin")))
				//registerMode("Sigma TMax", GridSettingMode(dry = True, type = "decimal", min = 0, max = 1, apply = applyField("s_tmax")))
				//registerMode("Sigma Noise", GridSettingMode(dry = True, type = "decimal", min = 0, max = 1, apply = applyField("s_noise")))
				//registerMode("Image Mask Weight", GridSettingMode(dry = True, type = "decimal", min = 0, max = 1, apply = applyField("inpainting_mask_weight")))
				foreach (var mode in new Dictionary<string, string> { { "var strength", "subseed_strength" }, { "denoising", "denoising_strength" }, { "ETA", "eta" }, { "sigma churn", "s_churn" }, { "sigma tmin", "s_tmin" }, { "sigma tmax", "s_tmax" }, { "sigma noise", "s_noise" }, { "Image Mask Weight", "inpainting_mask_weight" } })
				{
					registerMode(mode.Key, new(true, "decimal", mode.Value, 0, 1));
				}

				//registerMode("Out Width", GridSettingMode(dry = True, type = "integer", min = 0, apply = applyField("inf_grid_out_width")))
				//registerMode("Out Height", GridSettingMode(dry = True, type = "integer", min = 0, apply = applyField("inf_grid_out_height")))
				//registerMode("HighRes Scale", GridSettingMode(dry = True, type = "decimal", min = 1, max = 16, apply = applyField("hr_scale")))
				//registerMode("HighRes Steps", GridSettingMode(dry = True, type = "integer", min = 0, max = 200, apply = applyField("hr_second_pass_steps")))
				//registerMode("HighRes Upscaler", GridSettingMode(dry = True, type = "text", apply = applyField("hr_upscaler"), valid_list = lambda: list(map(lambda u: u.name, shared.sd_upscalers)) + list(shared.latent_upscale_modes.keys())))
				registerMode("highres upscaler", new(true, "text", "hr_upscaler", null, null, core.sd_upscalers, null));
			}
		}

		public object runsimple(object p, object grid_file, object output_file_path)
		{
			Console.WriteLine("Beggining section in c# code");
			Console.WriteLine(p.ToString());
			Console.WriteLine(grid_file.ToString());
			Console.WriteLine(output_file_path as string);
			//return null;
			return run(p, false, true, false, false, true, grid_file as string, true, output_file_path as string, null);
		}

		public Dictionary<string, object> a1111WebDataGetBaseParamData(dynamic p)
		{
			dynamic opts = Py.Import("modules.shared").GetItem("opts");
			dynamic shared = Py.Import("modules.shared");
			return new Dictionary<string, object>
			{
				//"sampler": p.sampler_name,
				{"sampler", p.sampler_name },
				//"seed": p.seed,
				{"seed", p.seed },
				//"restorefaces": (opts.face_restoration_model if p.restore_faces else None),
				{"restorefaces",  (p.restore_faces == null) ? opts.face_restoration_model : null},
				//"steps": p.steps,
				{"steps", p.steps },
				//"cfgscale": p.cfg_scale,
				{"cfgscale", p.cfg_scale },
				//"model": cs_core.chooseBetterFileName('', shared.sd_model.sd_checkpoint_info.model_name).replace(',', '').replace(':', ''),
				{"model", core.chooseBetterFileName("", (shared.sd_model.sd_checkpoint_info.model_name as string).Replace(",", "").Replace(":", "" )) },
				//"vae": (None if sd_vae.loaded_vae_file is None else (cs_core.chooseBetterFileName('', sd_vae.loaded_vae_file).replace(',', '').replace(':', ''))),
				{"vae", (opts.sd_vae == null) ? null : (core.chooseBetterFileName("", opts.sd_vae.loaded_vae_file) as string ).Replace(",", "").Replace(":", "") },
				//"width": p.width,
				{"width", p.width },
				//"height": p.height,
				{"height", p.height },
				//"prompt": p.prompt,
				{"prompt", p.prompt },
				//"negativeprompt": p.negative_prompt,
				{"negativeprompt", p.negative_prompt },
				//"varseed": (None if p.subseed_strength == 0 else p.subseed),
				{"varseed", (p.subseed_strength == 0) ? null : p.subseed },
				//"varstrength": (None if p.subseed_strength == 0 else p.subseed_strength),
				{"varstrength", (p.subseed_strength == 0) ? null : p.subseed },
				//"clipskip": opts.CLIP_stop_at_last_layers,
				{"clipskip", opts.CLIP_stop_at_last_layers },
				//"codeformerweight": opts.code_former_weight,
				{"codeformerweight", opts.code_former_weight },
				//"denoising": getattr(p, 'denoising_strength', None),
				{"denoising", p.denoising_strenght ?? null },
				//"eta": cs_core.fixNum(p.eta),
				{"eta", core.fixNum(p.eta) },
				//"sigmachurn": cs_core.fixNum(p.s_churn),
				{"sigmachurn", core.fixNum(p.s_churn) },
				//"sigmatmin": cs_core.fixNum(p.s_tmin),
				{"sigmatmin", core.fixNum(p.s_tmin) },
				//"sigmatmax": cs_core.fixNum(p.s_tmax),
				{"sigmatmin", core.fixNum(p.s_tmax) },
				//"sigmanoise": cs_core.fixNum(p.s_noise),
				{"sigmanoise", core.fixNum(p.s_noise) },
				//"ENSD": None if opts.eta_noise_seed_delta == 0 else opts.eta_noise_seed_delta
				{"ENSD", (opts.eta_noise_seed_delta == 0) ? null : opts.eta_noise_seed_delta }
			};
		}

		public object run(object? p, object? do_overwrite, object? generate_page, object? dry_run, object? validate_replace, object? publish_gen_metadata, string? grid_file, object? fast_skip, string? output_file_path, object? manualAxes)
		{
			//Console.WriteLine(InputPrompt.ToString() + do_overwrite.ToString() + generate_page.ToString() + dry_run.ToString() + validate_replace.ToString() + publish_gen_metadata.ToString() + grid_file.ToString() + fast_skip.ToString() + output_file_path.ToString() + manualAxes.ToString());
			imagecache = new();
			Console.WriteLine("new imagecache");
			tryinit();
			Console.WriteLine("inited");
			var processor = p as dynamic;
			processor.n_iter = 1;
			processor.do_not_save_samples = true;
			processor.do_not_save_grid = true;
			using (Py.GIL())
			{
				dynamic processing = Py.Import("modules.processing");
				processor.seed = processing.get_fixed_seed(processor.seed);
				processor.subseed = processing.get_fixed_seed(processor.subseed);
			}

			if (validate_replace is bool)
				validatereplace = validate_replace as bool? ?? false;

			if (grid_file.Contains("..") || grid_file == "")
				throw new Exception($"Unacceptable file name {grid_file}");
			if (output_file_path == null || output_file_path == default(string) || output_file_path == "")
			{
				output_file_path = "E:\\outputfolder";
			}
			if (output_file_path.Contains(".."))
				throw new Exception($"unacceptable alt file path{output_file_path}");
			if (grid_file == "Create In UI")
			{
				if (output_file_path == null || output_file_path == "") throw new Exception("Please specify output path");
				manualAxes = manualAxes as Tuple;
			}
			else manualAxes = null;
			GridGenCore core = new();
			var result = core.runGridGen(processor, grid_file, output_file_path, do_overwrite ?? false, fast_skip ?? true, generate_page ?? true, publish_gen_metadata ?? false, dry_run ?? false, manualAxes, this);
			return result;
		}

		public dynamic ui(bool is_img2img)
		{
			Py.GIL();
			core.listImageFiles();
			tryinit();
			dynamic refresh_button;
			dynamic grid_file;
			dynamic gr = Py.Import("gradio");
			grid_file = gr.dropdown("Select grid definition file", new string[] { "Create in UI" }.Concat(core.NameList).ToArray());

			dynamic opts = Py.Import("modules.shared.opts");
			dynamic output_file_path = gr.textbox("output folder name", "");
			dynamic page_will_be = gr.html("(…) <br><br>");
			dynamic manualGroup = gr.Group(visible: true);
			dynamic manualAxes = new List<dynamic>();
			dynamic sets = new List<dynamic>();
			dynamic do_overwrite;
			dynamic dry_run;
			dynamic fast_skip;
			dynamic generate_page;
			dynamic publish_gen_metadata;
			dynamic validate_replace;
			refresh_button = gr.ToolButton(value: "…", elem_id: "infinity_grid_refresh_button");
			refresh_button.click(fn: (PyObject)null, inputs: new PyObject[] { }, outputs: new PyObject[] { grid_file });
			gr.Row();
			gr.Column();
			int axisCount = 0;
			for (int group = 0; group < 4; group++)
			{
				using (dynamic groupObj = gr.Group())
				using (groupObj)
				{
					dynamic rows = new List<dynamic>();
					for (int i = 0; i < 4; i++)
					{
						using (gr.Row())
						{
							axisCount++;
							using (dynamic row_mode = gr.dropdown(label: $"Axis {axisCount} mode", choices: new string[] { "" }.Concat(this.validModes.Values.Select(x => x.name)).ToArray()))
							using (dynamic row_value = gr.textbox(label: $"axis {axisCount} value", lines: 1))
							using (dynamic fill_row_button = gr.ToolButton(value: "…", ComVisibleAttribute: false))
							{
								fill_row_button.click(fn: null, inputs: new string[] { row_mode }, outputs: new string[] { row_value });
								dynamic modeName = this.validModes[row_mode.value];
								dynamic buttonUpdate = gr.Button.update(ComVisibleAttribute: modeName is not null && (modeName.valid_list is not null || modeName.type == "boolean"));
								dynamic outFileUpdate = gr.Textbox.update() ?? gr.Textbox.update(value: $"autonamed_inf_grid_{DateTime.Now.ToString("dd_MM_yyyy")}");

								row_mode.change(fn: null, inputs: new string[] { row_mode, output_file_path }, outputs: new string[] { buttonUpdate, outFileUpdate });
								manualAxes.Add(row_mode);
								manualAxes.Add(row_value);
								rows.Add(row_mode);
							}
						}
					}
				}
			}
			using (gr.Row())
			{
				do_overwrite = gr.Checkbox(value: false, label: "overwrite existing images");
				dry_run = gr.Checkbox(value: false, label: "verify grid file");
				fast_skip = gr.Checkbox(value: true, label: "use performant skipping");
			}
			using (gr.Row())
			{
				generate_page = gr.Checkbox(value: true, label: "generate new webpage");
			}


			return new PyObject[] { do_overwrite, grid_file };
		}
	}
	public class GridSettingMode
	{
		public bool dry;
		public string type;
		public Action<object, object> apply;
		public float? min;
		public float? max;
		public List<string> valid_list;
		public Func<object, object, object> clean;
		public string name;

		public GridSettingMode(bool dry, string type, Action<object, object> apply, float? min = null, float? max = null, List<string> valid_list = null, Func<object, object, object> clean = null)
		{
			this.dry = dry;
			this.type = type;
			this.apply = apply;
			this.min = min;
			this.max = max;
			this.valid_list = valid_list;
			this.clean = clean;
		}

		public GridSettingMode(bool dry, string type, string fieldreplace, float? min = null, float? max = null, List<string> valid_list = null, Func<object, object, object> clean = null)
		{
			this.dry = dry;
			this.type = type;
			this.apply = ApplyField(fieldreplace);
			this.min = min;
			this.max = max;
			this.valid_list = valid_list;
			this.clean = clean;
		}

		public static Action<object, object> ApplyField(string fieldName)
		{
			return (p, v) =>
			{
				var fieldInfo = p.GetType().GetField(fieldName);
				fieldInfo.SetValue(p, v);
			};
		}
	}
	public class GridGenCore : IDisposable
	{
		List<string> imagecache;
		string assetdir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
		string imagedir;
		internal List<string> facerestorers
		{
			get
			{
				return gettitles(Py.Import("modules.shared").GetAttr("face_restorers"), "name");
			}
		}
		internal List<string> NameList { get { return Directory.GetFiles(assetdir, "*.yml", SearchOption.TopDirectoryOnly).ToList(); } }

		internal List<string> sd_upscalers
		{
			get
			{
				return gettitles(Py.Import("modules.shared").GetAttr("sd_upscalers"), "name");
			}
		}

		internal List<string> sd_samplers
		{
			get
			{
				return gettitles(Py.Import("modules").GetAttr("all_samplers_map.keys()"), "title");
			}
		}

		internal List<string> vaelist
		{
			get
			{
				return gettitles(Py.Import("modules").GetAttr("vae_dict.keys()"), "title");
			}
		}

		internal List<string> models
		{
			get
			{
				return gettitles(Py.Import("modules").GetAttr("checkpoints_list"), "title");
			}
		}

		private List<string> gettitles(dynamic list, string name)
		{
			var titles = new List<string>();
			var enumerator = list.GetIterator();
			while (enumerator.MoveNext())
			{
				var current = enumerator.Current;
				var title = current.GetAttr(name).ToString();
				titles.Add(title);
			}
			return titles;
		}

		internal object runGridGen(StableDiffusionProcessing processor, string grid_file, string output_file_path, object do_overwrite, object fast_skip, object generate_page, object publish_gen_metadata, object dry_run, object manualAxes, sdinfin parent)
		{
			GridFileHelper grid = null;
			//grid = GridFileHelper()
			//var grid = new GridFileHelper(processor, parent);
			//if manualPairs is None:
			if (manualAxes == null)
			{
				//	fullInputPath = ASSET_DIR + "/" + inputFile
				var fullInputPath = Path.Combine(assetdir, grid_file);
				//	if not os.path.exists(fullInputPath):
				//		raise RuntimeError(f"Non-existent file '{inputFile}'")
				//	# Parse and verify
				//	with open(fullInputPath, 'r') as yamlContentText:
				//		try:
				//			yamlContent = yaml.safe_load(yamlContentText)
				if (!System.IO.File.Exists(fullInputPath)) throw new Exception("Input file no longer exists.");

				//		except yaml.YAMLError as exc:
				//			raise RuntimeError(f"Invalid YAML in file '{inputFile}': {exc}")
				//	grid.parseYaml(yamlContent, inputFile)
				var deserializer = new DeserializerBuilder().Build();
				grid = deserializer.Deserialize<GridFileHelper>(System.IO.File.ReadAllText(fullInputPath));
				grid.processor = processor;
				grid.parent = parent;
			}
			else
			{
				/*
				//else:
				//	grid.title = outputFolderName

				//	grid.description = ""

				//	grid.variables = dict()

				//	grid.author = "Unspecified"

				//	grid.format = "png"

				//	grid.axes = list()

				//	grid.params = None

				var grid = new GridFileHelper(processor, parent);
				grid.title = output_file_path;
				grid.description = "";
				grid.variables = new Dictionary<string, object>();
				try
				{
					grid.author = Environment.UserName;
				}
				catch { grid.author = "unknown"; }
				grid.format = "png";
				grid.axes = new list<Axis>();

				//	for i in range(0, int(len(manualPairs) / 2)):

				//		key = manualPairs[i * 2]

				//		if isinstance(key, str) and key != "":
				//			try:
				//				grid.axes.append(Axis(grid, key, manualPairs[i * 2 + 1]))

				//			except Exception as e:
				//				raise RuntimeError(f"Invalid axis {(i + 1)} '{key}': errored: {e}")

				*/
			}
			//# Now start using it
			//if outputFolderName.strip() == "":
			//	outputFolderName = inputFile.replace(".yml", "")
			if (output_file_path == "")
			{
				output_file_path = Path.Combine(Path.GetDirectoryName(grid_file), Path.GetFileNameWithoutExtension(grid_file));
			}
			else
			{
				if (Path.IsPathRooted(output_file_path))
				{
					string outpath = output_file_path;
				}
				else { string outpath = Path.Combine(processor.outpath_grids, output_file_path); }
			}
			Directory.CreateDirectory(output_file_path);
			//#folder = outputFolderBase + "/" + outputFolderName
			//if os.path.isabs(outputFolderName):
			//	folder = outputFolderName

			//else:
			//	folder = os.path.join(outputFolderBase, outputFolderName)
			var runner = new GridRunner(grid, do_overwrite, output_file_path, processor, fast_skip);
			//runner = GridRunner(grid, doOverwrite, folder, passThroughObj, fastSkip)

			//runner.preprocess()

			//if generatePage:
			//	WebDataBuilder.EmitWebData(folder, grid, publishGenMetadata, passThroughObj)

			//result = runner.run(dryRun)
			

			//if dryRun:
			//	print("Infinite Grid dry run succeeded without error")

			//return result
		}

		public void Dispose()
		{
			imagecache = null;
			GC.Collect();
		}

		public List<string> listImageFiles()
		{
			if (imagecache != null)
				return imagecache;

			imagecache = new List<string>();
			imagedir = Path.Combine(assetdir, "images");
			if (!Directory.Exists(imagedir)) Directory.CreateDirectory(imagedir);
			var imageDir = CleanFilePath(Path.Combine(assetdir, "images"));
			imagecache = Directory.EnumerateFiles(imageDir, "*.*", SearchOption.AllDirectories)
									.Where(file => new[] { ".jpg", ".png", ".webp" }.Contains(Path.GetExtension(file)))
									.Select(file => CleanFilePath(file).Replace(imageDir, "").TrimStart('/'))
									.ToList();


			return imagecache;
		}

		private string CleanFilePath(string v)
		{
			return v.Replace("\\", "/").Replace("//", "/");
		}

		public string GetModelFor(string name)
		{
			return GetBestInList(name, models);
		}

		public string GetBestInList(string name, List<string> list)
		{
			string backup = null;
			int bestLen = 999;
			name = CleanName(name);
			foreach (string listVal in list)
			{
				string listValClean = CleanName(listVal);
				if (listValClean == name)
				{
					return listVal;
				}
				if (listValClean.Contains(name))
				{
					if (listValClean.Length < bestLen)
					{
						backup = listVal;
						bestLen = listValClean.Length;
					}
				}
			}
			return backup;
		}

		public static string CleanName(string name)
		{
			return name.ToLower().Replace(" ", "").Replace("[", "").Replace("]", "").Trim();
		}

		public object cleanModel(object arg1, object arg2)
		{
			var actualModel = GetModelFor(arg2 as string);
			if (actualModel == null) throw new Exception($"Invalid paramater {arg2} model name not recognized");
			return chooseBetterFileName(arg2 as string, actualModel);
		}

		public object chooseBetterFileName(string arg2, string actualModel)
		{
			string partial = Path.GetFileNameWithoutExtension(actualModel);
			if (arg2.Contains("/") || arg2.Contains("\\") || arg2.Contains(".") || arg2.Length >= partial.Length)
			{
				return arg2;
			}
			return partial;
		}

		public string getVaeFor(string vaeName)
		{
			using (Py.GIL())
			{
				return GetBestInList(vaeName, vaelist);
			}
		}

		public object cleanVae(object arg1, object arg2)
		{
			return chooseBetterFileName(arg1 as string, getVaeFor(CleanName(arg2 as string)));
		}

		internal object fixNum(dynamic num)
		{
			/*
			def fixNum(num):
				if num is None or math.isinf(num) or math.isnan(num):
					return None
				return num
			*/
			if (num == null) return null;
			try { return Convert.ToInt32(num); }
			catch { return null; }
		}

		internal static Dictionary<string, object> fixDict(Dictionary<object, object> baddict)
		{
			Dictionary<string, object> outdict = new();
			foreach (var a in baddict)
			{
				outdict.Add((a.Key as string).ToLower().Trim(), a.Value);
			}
			return outdict;
		}
	}
	internal class GridFileHelper
	{
		private Dictionary<string, string> variables;
		private List<Axis> axes;
		public string title { get; private set; }
		public string description { get; private set; }
		public string author { get; private set; }
		public string format { get; private set; }
		public Dictionary<string, object> parameters { get; private set; }
		public dynamic processor;
		public sdinfin parent;

		public GridFileHelper(dynamic processor, sdinfin parent)
		{
			this.processor = processor;
			this.parent = parent;
		}

		public string procVariables(string text)
		{
			if (text == null)
			{
				return null;
			}
			text = text.ToString();
			foreach (KeyValuePair<string, string> entry in variables)
			{
				text = text.Replace(entry.Key, entry.Value);
			}
			return text;
		}

		internal void validateParams(Dictionary<string, object> parameters)
		{
			foreach (var param in parameters)
			{
				parameters[param.Key] = validateSingleParam(param.Key, this.procVariables(param.Value as string));
			}
		}

		private object validateSingleParam(string key, string v)
		{
			var p = GridGenCore.CleanName(key);
			//def validateSingleParam(p: str, v):
			//   p = cs_class.cleanName(p)
			GridSettingMode mode;
			object result = null;
			try
			{
				mode = parent.validModes[p];
				var modeType = mode.type;
				if (modeType == "integer")
				{
					int vint = Convert.ToInt32(v);
					if (mode.min < vint && vint < mode.max)
					{
						result = v;
					}
					else throw new Exception($"value provided for {p} is out of range {mode.min} and {mode.max}");
				}
				else if (modeType == "decimal")
				{
					double vdec = Convert.ToDouble(v);
					if (mode.min < vdec && vdec < mode.max)
					{
						result = vdec;
					}
					else throw new Exception($"value provided for {p} is out of range {mode.min} and {mode.max}");
				}
				else if (modeType == "boolean")
				{
					result = tryconbool(v);
				}
				else if (modeType == "text")
				{
					if (v != null && v != "")
					{
						if (mode.clean != null) v = mode.clean(null, v).ToString();
						if (mode.valid_list != null && mode.valid_list.Contains(v.ToLower().Trim()))
						{
							result = new GridGenCore().GetBestInList(v, mode.valid_list);
						}
					}
				}
			}
			catch (Exception e) { }
			if (result != null)
				return result;
			else throw new Exception("Value was null");
		}

		private bool tryconbool(string temp)
		{
			if (temp == null) return false;
			if (temp == "1") return true;
			else if (temp == "0") return false;
			if (temp.ToLower().StartsWith("t")) return true;
			else return false;
		}

		public void parseYaml(Dictionary<object, object> yamlContenta, string grid_file)
		{
			variables = new Dictionary<string, string>();
			axes = new List<Axis>();
			Dictionary<string, object> yamlContent = GridGenCore.fixDict(yamlContenta);
			var varsObj = GridGenCore.fixDict((Dictionary<object, object>)yamlContent["variables"]);
			if (varsObj != null)
			{
				foreach (KeyValuePair<string, object> entry in varsObj)
				{
					variables[entry.Key.ToString().ToLower()] = entry.Value.ToString();
				}
			}
			var gridObj = GridGenCore.fixDict((Dictionary<object, object>)yamlContent["grid"]);
			if (gridObj == null)
			{
				throw new Exception("Invalid file " + grid_file + ": missing basic 'grid' root key");
			}

			title = procVariables((string)gridObj["title"]);
			description = procVariables((string)gridObj["description"]);
			author = procVariables((string)gridObj["author"]);
			format = procVariables((string)gridObj["format"]);
			if (title == null || description == null || author == null || format == null)
			{
				throw new Exception("Invalid file " + grid_file + ": missing grid title, author, format, or description in grid obj " + gridObj);
			}
			parameters = GridGenCore.fixDict((Dictionary<object, object>)gridObj["params"]);
			if (parameters != null)
			{
				validateParams(parameters);
			}
			Dictionary<string, object> axesObj = GridGenCore.fixDict((Dictionary<object, object>)yamlContent["axes"]);
			if (axesObj == null)
			{
				throw new Exception("Invalid file " + grid_file + ": missing basic 'axes' root key");
			}
			foreach (var entry in axesObj)
			{
				string id = entry.Key as string;
				object axisObj = entry.Value;
				try
				{
					axes.Add(new Axis(this, id, axisObj));
				}
				catch (Exception e)
				{
					throw new Exception("Invalid axis '" + id + "': errored: " + e);
				}
			}
			int totalCount = 1;
			foreach (Axis axis in axes)
			{
				totalCount *= axis.values.Count;
			}
			if (totalCount <= 0)
			{
				throw new Exception("Invalid file " + grid_file + ": something went wrong ... is an axis empty? total count is " + totalCount + " for " + axes.Count + " axes");
			}
			string cleanDesc = description.Replace('\n', ' ');
			Console.WriteLine("Loaded grid file, title '" + title + "', description '" + cleanDesc);
		}
	}

	internal class Axis
	{
		internal List<object> values;
		private GridFileHelper gridFileHelper;
		private string id;
		private object axisObj;

		public Axis(GridFileHelper gridFileHelper, string id, object axisObj)
		{
			this.gridFileHelper = gridFileHelper;
			this.id = id;
			this.axisObj = axisObj;
		}
	}
	public class GridRunner
	{
		GridGenCore grid;
		long totalRun;
		long totalSkip;
		long totalSteps;
		bool doOverwrite;
		string basePath;
		bool fastskip;
		dynamic promptskey;
		Dictionary<object, object> appliedsets;
//class GridRunner:
//    def __init__(self, grid: GridFileHelper, doOverwrite: bool, basePath: str, promptskey: StableDiffusionProcessing, fast_skip: bool):
//        self.grid = grid
//        self.totalRun = 0
//        self.totalSkip = 0
//        self.totalSteps = 0
//        self.doOverwrite = doOverwrite
//        self.basePath = basePath
//        self.fast_skip = fast_skip
//        self.promptskey = promptskey
//        self.applied_sets = {}

		GridRunner(GridGenCore grid, bool dooverwrite, string outpath, bool fastskip, dynamic processor)
		{
			this.grid = grid;
			this.doOverwrite = dooverwrite;
			this.fastskip = fastskip;
			this.basePath = outpath;
			this.promptskey = processor;
		}

		public List<object> buildValueSetList(List<Axis> axislist)
		{
			var result = new List<object>();
			if (axislist.Count == 0)
			{
				return result;
			}
			foreach(Axis axis in axislist)
			{
				if (axis.skip || !fastskip)
				{
					
				}
			}
		}
	//def buildValueSetList(self, axisList: list) -> list:
  //      result = list()
  //      if len(axisList) == 0:
  //          return result
		//curAxis = axisList[0]
  //      if len(axisList) == 1:
  //          for val in curAxis.values:
  //              if not val.skip or not self.fast_skip:

		//			newList = list()

		//			newList.append(val)
		//			result.append(SingleGridCall(newList))
  //          return result
		//nextAxisList = axisList[1::]
  //      for obj in self.buildValueSetList(nextAxisList):
  //          for val in curAxis.values:
  //              if not val.skip or not self.fast_skip:

		//			newList = obj.values.copy()

		//			newList.append(val)
		//			result.append(SingleGridCall(newList))
  //      return result

		//    def preprocess(self):
		//        self.valueSets = self.buildValueSetList(list(reversed(self.grid.axes)))
		//        print(f'Have {len(self.valueSets)} unique value sets, will go into {self.basePath}')
		//        for set in self.valueSets:
		//            set.filepath = self.basePath + '/' + '/'.join(list(map(lambda v: cs_class.cleanName(v.key), set.values)))
		//            set.data = ', '.join(list(map(lambda v: f"{v.axis.title}={v.title}", set.values)))
		//            set.flattenParams(self.grid)
		//            set.doSkip = set.skip or (not self.doOverwrite and os.path.exists(set.filepath + "." + self.grid.format))
		//            if set.doSkip:
		//                self.totalSkip += 1
		//            else:
		//                self.totalRun += 1
		//                stepCount = set.params.get("steps")
		//                self.totalSteps += int(stepCount) if stepCount is not None else self.promptskey.steps
		//        print(f"Skipped {self.totalSkip} files, will run {self.totalRun} files, for {self.totalSteps} total steps")

		//    def run(self, dry: bool):
		//        if gridRunnerPreRunHook is not None:
		//            gridRunnerPreRunHook(self)
		//        iteration = 0
		//        last = None
		//        prompt_batch_list = []
		//        for set in self.valueSets:
		//            if set.doSkip:
		//                continue
		//            iteration += 1
		//            if not dry:
		//                print(f'On {iteration}/{self.totalRun} ... Set: {set.data}, file {set.filepath}')
		//            p2 = copy(self.promptskey)
		//            if gridRunnerPreDryHook is not None:
		//                gridRunnerPreDryHook(self)
		//            set.applyTo(p2, dry)
		//            prompt_batch_list.append(p2)
		//            #self.applied_sets.add()
		//            self.applied_sets[p2] = self.applied_sets.get(p2, []) + [set]
		//        prompt_batch_list = self.batch_prompts(prompt_batch_list, self.promptskey)
		//        if not dry:
		//            for i, p2 in enumerate(prompt_batch_list):
		//                #print(f'On {i+1}/{len(prompt_batch_list)} ... Prompts: {p2.prompt[0]}')
		//                #p2 = StableDiffusionProcessing(p2)
		//                if p2 in modelchange.keys():
		//                    lateapplyModel(p2,modelchange[p2])
		//                if gridRunnerPreDryHook is not None:
		//                    gridRunnerPreDryHook(self)
		//                try:
		//                    last = gridRunnerRunPostDryHook(self, p2, self.applied_sets[p2])
		//                except: 
		//                    print("image failed to generate. please restart later")
		//                    continue
		//        return last

		//    def groupPrompts(self, prompt_list: list, Promptkey: StableDiffusionProcessing) -> list:
		//        prompt_groups = {}
		//        prompt_group = []
		//        if hasattr(prompt_list[0], "randomtime"):
		//            random_mode = prompt_list[0].randomtime
		//        else:
		//            random_mode = "none"
		//        if random_mode == "contant":
		//            random.shuffle(prompt_list)
		//            print("todo: always")
		//        elif random_mode == "sort":
		//            for iter, prompt in prompt_list:
		//                if "lora" in prompt.prompt:
		//                    print("todo: lora sorting")
		//            print("todo: sort")
		//        if random_mode == "bymodel":
		//            print("todo: bymodel")


		//    def batch_prompts(self, prompt_list: list, Promptkey: StableDiffusionProcessing) -> list:
		//        # Group prompts by batch size
		//        prompt_groups = {}
		//        prompt_group = []
		//        batchsize = Promptkey.batch_size
		//        starto = 0
		//        if hasattr(prompt_list[0], "randomtime"):
		//            random_mode = prompt_list[0].randomtime
		//        else:
		//            random_mode = "none"
		//        prompt_groups = {}
		//        prompt_group = []
		//        starto = 0
		//        devid = torch.cuda.current_device
		//        new_list = []
		//        for prompt in prompt_list.copy():
		//            if not hasattr(prompt, "deviceidpriority"): 
		//                #print("assuming default")
		//                prompt.deviceidpriority = devid
		//            if devid != prompt.deviceidpriority:
		//                #print("moving to bottom")
		//                prompt_list.remove(prompt)
		//                new_list.append(prompt)
		//        prompt_list.extend(new_list)
		//        newlist = []
		//        for prompt in prompt_list.copy():
		//            prompt.deviceidpriority = None
		//            newlist.append(prompt)
		//        prompt_list = newlist


		//        for iterator, prompt in enumerate(prompt_list):
		//            if iterator > 0:
		//                prompt2 = prompt_list[iterator - 1]
		//            else:
		//                prompt2 = prompt
		//            if prompt in modelchange and prompt != prompt2 and prompt2 in modelchange and modelchange[prompt] != modelchange[prompt2]:
		//                if len(prompt_group) > 0:
		//                    prompt_groups[starto] = prompt_group
		//                    starto += 1
		//                prompt_groups[starto] = prompt
		//                starto += 1
		//                prompt_group = []
		//            elif iterator % prompt.batch_size == 0:
		//                if prompt_group:
		//                    prompt_groups[starto] = prompt_group
		//                    starto += 1
		//                    prompt_group = []
		//                prompt_group.append(prompt)
		//            else:
		//                prompt_group.append(prompt)
		//        if prompt_group:
		//            prompt_groups[starto] = prompt_group

		//        if random_mode == "bymodel":
		//            # Start a new group when a modelchange occurs and add a randomizer
		//            print("randomizing order")
		//            starto = 0
		//            new_groups = {}
		//            last_model = None
		//            for key, group in prompt_groups.items():
		//                if isinstance(group, list):
		//                    new_group = []
		//                    for prompt in group:
		//                        if prompt in modelchange and modelchange[prompt] != last_model:
		//                            if new_group:
		//                                random.shuffle(new_group)
		//                                new_groups[starto] = new_group
		//                                starto += 1
		//                                new_group = []
		//                            last_model = modelchange[prompt]
		//                        new_group.append(prompt)
		//                    if new_group:
		//                        random.shuffle(new_group)
		//                        new_groups[starto] = new_group
		//                        starto += 1
		//                else:
		//                    new_groups[key] = group
		//            if prompt_group:
		//                prompt_groups[starto] = new_group
		//            prompt_groups = new_groups

		//        elif random_mode == "constant":
		//            # Group prompts before randomization and add a randomizer within each group
		//            grouped_prompts = {}
		//            for prompt in prompt_list:
		//                if prompt.prompt_key.randomtime in modelchange:
		//                    model = modelchange[prompt.prompt_key.randomtime]
		//                    if model not in grouped_prompts:
		//                        grouped_prompts[model] = []
		//                    grouped_prompts[model].append(prompt)
		//                else:
		//                    if "default" not in grouped_prompts:
		//                        grouped_prompts["default"] = []
		//                    grouped_prompts["default"].append(prompt)
		//            new_groups = {}
		//            for model, group in grouped_prompts.items():
		//                if len(group) > 0:
		//                    random.shuffle(group)
		//                    for iterator in range(0, len(group), prompt.batch_size):
		//                        new_groups[starto] = group[iterator:iterator+prompt.batch_size]
		//                        starto += 1
		//            prompt_groups = new_groups

		//        print("added all to groups")
		//        merged_prompts = []
		//        print(f"there are {len(prompt_groups)} groups after grouping. merging now")
		//        for iterator, promgroup in enumerate(prompt_groups):
		//            if iterator in prompt_groups:
		//                promgroup = prompt_groups[iterator]
		//            else: continue
		//            #print(type(promgroup))
		//            if isinstance(promgroup, StableDiffusionProcessing) or isinstance(promgroup, int):
		//                print("object is processing object")
		//                fail = True
		//            else:
		//                fail = False
		//                prompt_attr = promgroup[0]
		//                batchsize = prompt_attr.batch_size
		//                print(f"merging prompts {iterator*batchsize} - {iterator*batchsize+batchsize} of {len(prompt_groups.items())*batchsize}")

		//                for it, tempprompt in enumerate(promgroup):
		//                    #print(tempprompt)

		//                    if not all(hasattr(tempprompt2, attr) for tempprompt2 in promgroup for attr in dir(tempprompt)):
		//                        fail = True
		//                        print(f"prompt does not contain {str(attr)} can not merge")
		//                        break
		//                    for attr in dir(tempprompt):
		//                        if attr.startswith("__"): continue
		//                        if callable(getattr(tempprompt, attr)): continue
		//                        if isinstance(getattr(tempprompt, attr, None), types.BuiltinFunctionType) or isinstance(getattr(tempprompt, attr, None), types.BuiltinMethodType): continue
		//                        if attr in ['prompt', 'all_prompts', 'all_negative_prompts', 'negative_prompt', 'seed', 'subseed']: continue
		//                        try:
		//                            if getattr(tempprompt, attr) == getattr(prompt_attr, attr): continue
		//                            else: 
		//                                fail = True
		//                                if it == 1: print(f"Prompt contains incorrect {str(attr)} merge unavailable. values are: {str(getattr(tempprompt, attr))}")
		//                                print(f"prompt contains incorrect {str(attr)} merge unavailable. values are: {str(getattr(prompt_attr, attr))}")
		//                                break
		//                        except AttributeError:
		//                            print(tempprompt)
		//                            print(prompt_attr)
		//                            raise
		//            if not fail:
		//                merged_prompt = prompt_attr
		//                merged_prompt.prompt = [p.prompt for p in promgroup]
		//                merged_prompt.negative_prompt = [p.negative_prompt for p in promgroup]
		//                merged_prompt.seed = [p.seed for p in promgroup]
		//                merged_prompt.subseed = [p.subseed for p in promgroup]
		//                merged_prompts.append(merged_prompt)
		//                # Add applied sets
		//                for prompt in promgroup:
		//                    setup2 = self.applied_sets.get(prompt, [])
		//                    #print(setup2)
		//                    merged_filepaths = [setup.filepath for setup in self.applied_sets[merged_prompt]]
		//                    if any(setall.filepath in merged_filepaths for setall in setup2): continue
		//                    if self.applied_sets.get(prompt, []) in self.applied_sets[merged_prompt]: continue
		//                    self.applied_sets[merged_prompt] += self.applied_sets.get(prompt, [])
		//                #print("merged")
		//                merged_prompt.batch_size = len(promgroup)

		//            if fail and (isinstance(promgroup, StableDiffusionProcessingTxt2Img) or isinstance(promgroup, StableDiffusionProcessing) or isinstance(promgroup, StableDiffusionProcessingImg2Img)):
		//                promgroup.batch_size = 1
		//                merged_prompts.append(promgroup)
		//            elif fail and (isinstance(promgroup, int)):
		//                continue
		//            elif fail:
		//                for prompt in promgroup:
		//                    prompt.batch_size = 1
		//                merged_prompts.extend(promgroup)
		//        print(f"there are {len(merged_prompts)} generations after merging")
		//        return merged_prompts

	}
}

