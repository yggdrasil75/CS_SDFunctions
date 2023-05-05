using Microsoft.SqlServer.Server;
using Newtonsoft.Json;
using Python.Runtime;
using System;
using System.Collections.Generic;
using System.Diagnostics.Eventing.Reader;
using System.IO;
using System.Linq;
using System.Net.Configuration;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Remoting.Services;
using System.Security.Cryptography;
using System.Security.Policy;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Linq;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.Schemas;

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
		public GridGenCore core;

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
			core = new GridGenCore(this);
		}

		internal void applyModel(object arg1, object arg2)
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
				dynamic restorer = GridGenCore.GetBestInList(input, core.facerestorers);
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
			if (val.Length != 2) throw new Exception("invalid promgroup replace. need a single =");
			string match = val[0].Trim();
			string replace = val[1].Trim();
			if (validatereplace && !InputPrompt.Contains(match)) throw new Exception("Match not in promgroup");
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
				//registerMode("Model", GridSettingMode(dry = False, type = "text", apply = applyModel, clean = cleanModel, valid_list = lambda: list(map(lambda m: m.Title, sd_models.checkpoints_list.values()))))
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
				registerMode("promgroup replace", new GridSettingMode(true, "text", applyPromptReplace));
				//registerMode("Negative Prompt Replace", GridSettingMode(dry = True, type = "text", apply = applyNegPromptReplace))
				registerMode("negative promgroup replace", new GridSettingMode(true, "text", applyNegPromptReplace));
				//registerMode("N Prompt Replace", GridSettingMode(dry = True, type = "text", apply = applyNegPromptReplace))
				registerMode("n promgroup replace", new GridSettingMode(true, "text", applyNegPromptReplace));
				for (int i = 0; i < 10; i++)
				{
					//registerMode("Prompt Replace", GridSettingMode(dry = True, type = "text", apply = applyPromptReplace))
					registerMode($"promgroup replace {i}", new GridSettingMode(true, "text", applyPromptReplace));
					//registerMode("Negative Prompt Replace", GridSettingMode(dry = True, type = "text", apply = applyNegPromptReplace))
					registerMode($"negative promgroup replace {i}", new GridSettingMode(true, "text", applyNegPromptReplace));
					//registerMode("N Prompt Replace", GridSettingMode(dry = True, type = "text", apply = applyNegPromptReplace))
					registerMode($"n promgroup replace{i}", new GridSettingMode(true, "text", applyNegPromptReplace));
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

				//modes = ["promgroup", "negative promgroup", "random"]
				//fields = ["promgroup", "negative_prompt", "randomtime"]
				//for field, mode in enumerate(modes):
				//	registerMode(mode, GridSettingMode(dry = True, type = "text", apply = applyField(fields[field])))
				foreach (var mode in new Dictionary<string, string> { { "promgroup", "promgroup" }, { "negative promgroup", "negative_prompt" }, { "random", "randomtime" }, { "batch size", "batch_size" } })
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
				//TODO: finish with high res stuff. 
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

		public Dictionary<string, object> webDataGetBaseParamData(dynamic p)
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
				//"promgroup": p.promgroup,
				{"promgroup", p.prompt },
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

		public object run(object? p, bool? doOverwrite, bool? GeneratePage, bool? dryRun, bool? validateReplace, bool? publish_gen_metadata, string? grid_file, bool? fast_skip, string? output_file_path, List<Object>? manualAxes)
		{
			//Console.WriteLine(InputPrompt.ToString() + doOverwrite.ToString() + GeneratePage.ToString() + dryRun.ToString() + validateReplace.ToString() + publish_gen_metadata.ToString() + grid_file.ToString() + fast_skip.ToString() + output_file_path.ToString() + manualAxes.ToString());
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

			if (validateReplace is bool)
				validatereplace = validateReplace as bool? ?? false;

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
			}
			else manualAxes = null;
			//GridGenCore core = new(this);
			var result = core.RunGridGen(processor, grid_file, output_file_path, this, output_file_path, doOverwrite ?? false, fast_skip ?? true, GeneratePage ?? true, publish_gen_metadata ?? false, dryRun ?? false, manualAxes);
			return result;
		}

		public dynamic ui(bool is_img2img)
		{
			//TODO: UI requires use of python still. need to figure out why and fix it.
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
		sdinfin sdfin;
		internal string title;
		internal string description;
		internal string author;
		internal Dictionary<object, object> modelchange = new();

		internal List<Axis> axes;

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
				return gettitles(Py.Import("modules").GetAttr("all_samplers_map.keys()"), "Title");
			}
		}

		internal List<string> vaelist
		{
			get
			{
				return gettitles(Py.Import("modules").GetAttr("vae_dict.keys()"), "Title");
			}
		}

		internal List<string> models
		{
			get
			{
				return gettitles(Py.Import("modules").GetAttr("checkpoints_list"), "Title");
			}
		}

		public GridGenCore(sdinfin sdfin)
		{
			this.sdfin = sdfin;
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

		public object RunGridGen(StableDiffusionProcessing passThroughObj, string inputFile, string outputFolderBase, sdinfin sdinfin, string outputFolderName = null, bool doOverwrite = false, bool fastSkip = false, bool generatePage = true, bool publishGenMetadata = true, bool dryRun = false, List<object> manualPairs = null)
		{
			var grid = new GridFileHelper(passThroughObj, sdinfin);
			if (manualPairs == null)
			{
				string fullInputPath = assetdir + "/" + inputFile;
				if (!File.Exists(fullInputPath))
				{
					throw new Exception($"Non-existent file '{inputFile}'");
				}
				// Parse and verify
				var deserializer = new DeserializerBuilder().Build();
				using (var reader = new StreamReader(fullInputPath))
				{
					var yamlContent = deserializer.Deserialize(reader);
					grid.parseYaml(yamlContent as Dictionary<object, object>, inputFile);
				}
			}
			else
			{
				//TODO
			}
			// Now start using it
			if (string.IsNullOrWhiteSpace(outputFolderName))
			{
				outputFolderName = inputFile.Replace(".yml", "");
			}
			string folder;
			if (Path.IsPathRooted(outputFolderName))
			{
				folder = outputFolderName;
			}
			else
			{
				folder = Path.Combine(outputFolderBase, outputFolderName);
			}
			var runner = new GridRunner(grid, doOverwrite, folder, passThroughObj, fastSkip);
			runner.Preprocess();
			if (generatePage)
			{
				WebDataBuilder databuilder = new WebDataBuilder();
				databuilder.EmitWebData(folder, grid, publishGenMetadata, passThroughObj, this.sdfin);
			}
			var result = runner.run(dryRun);
			if (dryRun)
			{
				Console.WriteLine("Infinite Grid dry run succeeded without error");
			}
			return result;
		}

		public static string? cleanForWeb(string unclean)
		{
			if (unclean == null) return "";
			if (unclean.GetType() != typeof(string)) throw new Exception("value is not text.");
			return unclean.Replace("\"", "&quot;");
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

		public static string GetBestInList(string name, List<string> list)
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

		internal static string cleanID(string id)
		{
			id = id.ToLower().Trim();
			id = Regex.Replace(id, "[^a-z0-9]", "_");
			return id;
		}

		internal static List<string> expandNumericListRanges(List<string> valuesList, Type type)
		{
			throw new NotImplementedException();
		}

		List<string> replacements = new();
		List<string> nreplacements = new();
		internal bool GridCallParamAddHook(string p, string v)
		{
			string temp = CleanName(p);
			if (temp.StartsWith("promptreplace"))
			{
				this.replacements.Add(v);
				return true;
			}
			return false;
		}
		internal bool GridCallParamAddHookneg(string p, string v)
		{
			string temp = CleanName(p);
			if (temp.StartsWith("npromptreplace") || temp.StartsWith("negativepromptreplace"))
			{
				this.replacements.Add(v);
				return true;
			}
			return false;
		}
		internal dynamic GridRunnerRunPostDryHook(dynamic promptkey, Dictionary<string, object> appliedsets)
		{
			using (Py.GIL())
			{
				dynamic processing = Py.Import("modules.processing");
				promptkey.seed = processing.get_fixed_seed(promptkey.seed);
				promptkey.subseed = processing.get_fixed_seed(promptkey.subseed);
				dynamic process_images = Py.Import("modules.processing.process_images");
				dynamic processed = process_images(promptkey);

				if (processed.images.Count < 1)
				{
					throw new Exception("something went wrong. produced no images, generation failed.");
				}
				Console.WriteLine($"There are {processed.images.Count} images being printed.");


				return processed;
			}
		}

		internal void GridRunnerPreDryHook(GridRunner gridRunner)
		{
			throw new NotImplementedException();
		}
	}

	internal class GridFileHelper
	{
		internal Dictionary<string, string> variables;
		internal List<Axis> axes;
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

		internal void validateParams(Dictionary<string, string> parameters)
		{
			foreach (var param in parameters)
			{
				parameters[param.Key] = validateSingleParam(param.Key, this.procVariables(param.Value));
			}
		}

		internal string validateSingleParam(string key, string v)
		{
			var p = GridGenCore.CleanName(key);
			//def validateSingleParam(p: str, v):
			//   p = cs_class.cleanName(p)
			GridSettingMode mode;
			string result = null;
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
						result = vdec.ToString();
					}
					else throw new Exception($"value provided for {p} is out of range {mode.min} and {mode.max}");
				}
				else if (modeType == "boolean")
				{
					result = tryconbool(v).ToString();
				}
				else if (modeType == "text")
				{
					if (v != null && v != "")
					{
						if (mode.clean != null) v = mode.clean(null, v).ToString();
						if (mode.valid_list != null && mode.valid_list.Contains(v.ToLower().Trim()))
						{
							result = GridGenCore.GetBestInList(v, mode.valid_list);
						}
					}
				}
			}
			catch (Exception e) { }
			if (result != null)
				return result as string;
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
				throw new Exception("Invalid file " + grid_file + ": missing basic 'grid' root Key");
			}

			title = procVariables((string)gridObj["Title"]);
			description = procVariables((string)gridObj["Description"]);
			author = procVariables((string)gridObj["author"]);
			format = procVariables((string)gridObj["format"]);
			if (title == null || description == null || author == null || format == null)
			{
				throw new Exception("Invalid file " + grid_file + ": missing grid Title, author, format, or Description in grid obj " + gridObj);
			}
			parameters = GridGenCore.fixDict((Dictionary<object, object>)gridObj["params"]);
			if (parameters != null)
			{
				validateParams(parameters.ToDictionary(entry => entry.Key, entry => entry.Value as string));
			}
			Dictionary<string, object> axesObj = GridGenCore.fixDict((Dictionary<object, object>)yamlContent["axes"]);
			if (axesObj == null)
			{
				throw new Exception("Invalid file " + grid_file + ": missing basic 'axes' root Key");
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
			Console.WriteLine("Loaded grid file, Title '" + title + "', Description '" + cleanDesc);
		}
	}
	//second try starts here.
	internal class SettingsFixer
	{
		object model;
		object clip_stop_at_last_layers;
		object code_former_weight;
		object face_restoration_model;
		object eta_noise_seed_delta;
		object vae;
		internal void enter()
		{
			dynamic opts = Py.Import("modules.opts");
			model = opts.sd_model_checkpoint;
			clip_stop_at_last_layers = opts.CLIP_stop_at_last_layers;
			code_former_weight = opts.code_former_weight;
			face_restoration_model = opts.face_restoration_model;
			eta_noise_seed_delta = opts.eta_noise_seed_delta;
			vae = opts.sd_vae;
		}
		internal void exit()
		{
			dynamic opts = Py.Import("modules.opts");
			opts.sd_model_checkpoint = model;
			opts.CLIP_stop_at_last_layers = clip_stop_at_last_layers;
			opts = opts.code_former_weight;
			opts.face_restoration_model = face_restoration_model;
			opts.eta_noise_seed_delta = eta_noise_seed_delta;
		}
	}
	internal class WebDataBuilder
	{
		string html;
		string assetdir;
		sdinfin sdinfin1;
		internal string buildJson(dynamic grid, bool publish_gen_metadata, dynamic processor)
		{
			var result = new Dictionary<object, object>();
			result["Title"] = grid.title;
			result["Description"] = grid.description;
			result["ext"] = grid.format;
			if (publish_gen_metadata)
				result["metadata"] = sdinfin1.webDataGetBaseParamData(processor);
			var axes = new List<object>();
			foreach (Axis axis in grid.axes)
			{
				var jaxis = new Dictionary<string, object>();
				jaxis["id"] = (axis.id as string).ToLower();
				jaxis["Title"] = axis.title;
				jaxis["Description"] = axis.description;
				var values = new List<Dictionary<string, object>>();
				foreach (var val in axis.values)
				{
					var jval = new Dictionary<string, object>();
					jval["Key"] = (val.Key as string).ToLower();
					jval["Title"] = val.Title;
					jval["descrption"] = val.Description;
					jval["Show"] = val.Show;
					if (publish_gen_metadata) jval["params"] = val.Parameters;
					values.Add(jval);
				}
				jaxis["values"] = values;
				axes.Append(jaxis);
			}
			result["axes"] = axes;

			return JsonConvert.SerializeObject(result, Formatting.Indented);
		}

		public static string radioButtonHtml(string name, string id, string descrip, string label)
		{
			return $"<input type=\"radio\" class=\"btn-check\" name=\"{name}\" id=\"{id.ToLower()}\" autocomplete=\"off\" checked=\"\">" +
				$"<label class=\"btn btn-outline-primary\" for=\"{id.ToLower()}\" Title=\"{descrip}\">{label}</label>\\n";
		}

		public static string axisBar(string label, string content)
		{
			return $"<br><div class=\"btn-group\" role=\"group\" aria-label=\"Basic radio toggle button group\">{label}:&nbsp;\\n{content}</div>\\n";
		}

		public string buildHTML(GridGenCore grid)
		{
			html = System.IO.File.ReadAllText(Path.Combine(assetdir, "page.html"));
			string xselect = "";
			string yselect = "";
			var x2select = WebDataBuilder.radioButtonHtml("x2_axis_selector", "x2_none", null, null);
			var y2select = WebDataBuilder.radioButtonHtml("y2_axis_selector", "y2_none", null, null);
			string content = "<div style=\"margin: auto; width: fit-content;\"><table class=\"sel_table\">\\n";
			string advancedSettings = "";
			bool primary = true;
			foreach (Axis axis in grid.axes)
			{
				string axisdescrip = GridGenCore.cleanForWeb(axis.description);
				var trClass = primary ? "primary" : "secondary";
				content += "<tr class=\"{trClass}\">\\n<td>\\n<h4>{axis.Title}</h4>\\n";
				advancedSettings += $"\\n<h4>{axis.title}</h4><div class=\"timer_box\">Auto cycle every " +
					$"<input style=\"width:30em;\" autocomplete=\"off\" type=\"range\" min=\"0\" max=\"360\" value=\"0\" class=\"form-range timer_range\" id=\"range_tablist_{axis.id}\">" +
					$"<label class=\"form-check-label\" for=\"range_tablist_{axis.id}\" id=\"label_range_tablist_{axis.id}\">0 seconds</label></div>\\nShow value: ";
				string axisClass = "axis_table_cell";
				if (axisdescrip.Trim().Length == 0)
					axisClass += " emptytab";
				content += $"<div class=\"{axisClass}\">{axisdescrip}</div></td>\\n<td><ul class=\"nav nav-tabs\" role=\"tablist\" id=\"tablist_{axis.id}\">\\n";
				primary = !primary;
				bool isFirst = Convert.ToBoolean(axis.defaultVal);
				foreach (var val in axis.values)
				{
					if (axis.defaultVal != null)
						isFirst = axis.defaultVal == val.Key;
					string selected = isFirst ? "true" : "false";
					string active = isFirst ? " active" : "";
					isFirst = false;
					string descrip = GridGenCore.cleanForWeb(val.Description);
					content += $"<li class=\"nav-item\" role=\"presentation\"><a class=\"nav-link{active}\" Data-bs-toggle=\"tab\" href=\"#tab_{axis.id}__{val.Key}\" id=\"clicktab_{axis.id}__{val.Key}\"" +
						$" aria-selected=\"{selected}\" role=\"tab\" Title=\"{val.Title}: {descrip}\">{val.Title}</a></li>\\n";
					advancedSettings += $"&nbsp;<input class=\"form-check-input\" type=\"checkbox\" autocomplete=\"off\" id=\"showval_{axis.id}__{val.Key}\" checked=\"true\" onchange=\"" +
						$"javascript:toggleShowVal(\\'{axis.id}\\', \\'{val.Key}\\')\"> <label class=\"form-check-label\" for=\"showval_{axis.id}__{val.Key}\" " +
						$"Title=\"Uncheck this to hide \\'{val.Title}\\' from the page.\">{val.Title}</label>";
				}
				advancedSettings += $"&nbsp;&nbsp;<button class=\"submit\" onclick=\"javascript:toggleShowAllAxis(\\'{axis.id}\\')\">Toggle All</button>";
				content += "</ul>\\n<div class=\"tab-content\">\\n";
				isFirst = Convert.ToBoolean(axis.defaultVal);
				foreach (var val in axis.values)
				{
					string active = isFirst ? " active Show" : "";
					isFirst = false;
					string descrip = GridGenCore.cleanForWeb(val.Description);
					if (descrip.Length == 0)
						active += " emptyTab";
					content += $"<div class=\"tab-pane{active}\" id=\"tab_{axis.id}__{val.Key}\" role=\"tabpanel\"><div class=\"tabval_subdiv\">{descrip}</div></div>\\n";
				}
				xselect += WebDataBuilder.radioButtonHtml("x_axis_select", $"x_{axis.id}", axisdescrip, axis.title);
				yselect += WebDataBuilder.radioButtonHtml("y_axis_select", $"y_{axis.id}", axisdescrip, axis.title);
			}
			content += "</table>\\n<div class=\"axis_selectors\">";
			content += WebDataBuilder.axisBar("X Axis", xselect);
			content += WebDataBuilder.axisBar("Y Axis", yselect);
			content += WebDataBuilder.axisBar("x Super-Axis", x2select);
			content += WebDataBuilder.axisBar("Y Super-Axis", y2select);
			content += "</div></div>\n";
			content += "<div><center><input class=\"form-check-input\" type=\"checkbox\" autocomplete=\"off\" value=\"\" id=\"autoScaleImages\"> <label class=\"form-check-label\" for=\"autoScaleImages\">Auto-scale images to viewport width</label></center></div>";
			content += "<div style=\"margin: auto; width: fit-content;\"><table id=\"image_table\"></table></div>\\n";
			html = html.Replace("{TITLE}", grid.title).Replace("{CLEAN_DESCRIPTION}", GridGenCore.cleanForWeb(grid.description)).Replace("{DESCRIPTION}", grid.description).Replace("{CONTENT}", content).Replace("{ADVANCED_SETTINGS}", advancedSettings).Replace("{AUTHOR}", grid.author).Replace("{EXTRA_FOOTER}", "");
			return html;
		}

		public void EmitWebData(string path, GridFileHelper grid, bool publish_gen_metadata, dynamic p, sdinfin sdfin)
		{
			sdinfin1 = sdfin;
			Console.WriteLine("Building final web Data...");
			Directory.CreateDirectory(path);
			string json = this.buildJson(grid, publish_gen_metadata, p);
			string f = Path.Combine(path, "Data.js");
			File.WriteAllText(f, json);
			foreach (string f2 in new List<string>() { "bootstrap.min.css", "bootstrap.bundle.min.js", "proc.js", "jquery.min.js" })
			{
				File.Copy(Path.Combine(assetdir, f2), Path.Combine(path, f2));
			}
			File.WriteAllText(Path.Combine(path, "index.html"), html);
		}
	}
	internal class GridRunner
	{
		GridFileHelper grid;
		long totalRun = 0;
		long totalSkip = 0;
		long totalSteps = 0;
		bool doOverwrite;
		string basePath;
		bool fastskip;
		dynamic promptsKey;
		Dictionary<dynamic, List<AxisValue>> appliedSets;
		List<SingleGridCall> valueSets;

		public GridRunner(GridFileHelper grid, bool dooverwrite, string basepath, dynamic promptskey, bool fastSkip)
		{
			this.grid = grid;
			this.doOverwrite = dooverwrite;
			this.basePath = basepath;
			this.fastskip = fastskip;
			this.promptsKey = promptskey;
			this.appliedSets = new();
		}

		public List<AxisValue> BuildValueSetsInner(Axis curAxis)
		{
			var result = new List<AxisValue>();
			foreach (var val in curAxis.values)
				if (!val.Skip || !this.fastskip)
					result.Add(val);
			return result;
		}

		public void buildValueSetList(List<Axis> axislist)
		{
			var result = new List<SingleGridCall>();
			if (axislist.Count == 0)
				return;
			if (axislist.Count == 1)
			{
				BuildValueSetsInner(axislist[0]);
			}
			Axis curAxis = axislist[0];
			foreach (var obj in BuildValueSetsInner(axislist[0]))
			{
				List<AxisValue> newlist = new List<AxisValue>();
				foreach (var val in curAxis.values)
				{
					if (!val.Skip || !this.fastskip)
					{
						newlist.Add(val);
					}
				}
				result.Append(new SingleGridCall(newlist));
			}
			valueSets = result;
		}

		public void Preprocess()
		{
			grid.axes.Reverse();
			buildValueSetList(grid.axes);
			Console.WriteLine($"Have {valueSets.Count} unique value sets, will go into {this.basePath}");

			foreach (var set in valueSets)
			{
				set.value.Filepath = basePath + '/' + string.Join("/", set.values.Select(v => GridGenCore.CleanName(v.Key)).ToList());
				set.value.Data = string.Join(", ", set.values.Select(v => $"{v.axis.title}={v.Title}").ToList());

				set.flattenParams(grid);
				set.skip = set.skip || (!this.doOverwrite && File.Exists(set.value.Filepath + "." + grid.format));

				if (set.skip)
				{
					totalSkip += 1;
				}
				else
				{
					totalRun += 1;
					var stepCount = set.parameters.ContainsKey("steps") ? set.parameters["steps"] : promptsKey.steps;
					totalSteps += int.Parse(stepCount ?? "0");
				}
			}

			Console.WriteLine($"Skipped {totalSkip} files, will run {totalRun} files, for {totalSteps} total steps");
		}

		public object run(bool dry)
		{
			grid.parent.core.GridRunnerPreDryHook(this);
			int iteration = 0;
			dynamic last = new object();
			var promptBatchList = new List<dynamic>();
			foreach (var set in valueSets)
			{
				if (set.skip) continue;
				iteration += 1;
				if (!dry) Console.WriteLine($"on {iteration}/ {totalRun} … Set: {set.value.Data}, file: {set.value.Filepath}");
				var p2 = promptsKey.MemberwiseClone();
				grid.parent.core.GridRunnerPreDryHook(this);
				set.applyTo(p2, dry);
				promptBatchList.Add(p2);
				appliedSets[p2] = appliedSets.ContainsKey(p2) ? appliedSets[p2].Concat(new List<AxisValue> { set.value }).ToList() : new List<AxisValue> { set.value };
			}
			promptBatchList = batchPrompts(promptBatchList, promptsKey);
			if (dry)
			{
				for (var i = 0; i < promptBatchList.Count; i++)
				{
					var p2 = promptBatchList[i];
					grid.parent.applyModel(p2, grid.parent.core.modelchange[p2]);
					grid.parent.core.GridRunnerPreDryHook(this);
					try
					{
						last = grid.parent.core.GridRunnerRunPostDryHook(p2, this.appliedSets[p2]);
					}
					catch { Console.WriteLine("Failed to generate image. restart later"); }
				}
			}
			return last;
		}

		public List<object> batchPrompts(List<dynamic> promptbatchlist, dynamic prompkey)
		{
			var promptGroups = new Dictionary<object, object>();
			var promptGroup = new List<object>();
			int batchsize = promptbatchlist[0].batch_size;
			int starto = 0;
			for (int i = 0; i < promptbatchlist.Count; i++)
			{
				dynamic prompt2;
				var prompt = promptbatchlist[i];
				if (i > 0)
					prompt2 = promptbatchlist[i - 1];
				else prompt2 = prompt;
				if (grid.parent.core.modelchange[prompt] != grid.parent.core.modelchange[prompt2])
				{
					promptGroups[starto] = promptGroup;
					promptGroup = new List<object>();
					starto++;
				}
				else if (i == promptbatchlist.Count)
				{
					promptGroup.Add(prompt);
					promptGroups[starto] = promptGroup;
				}
				else if (i % prompt.batch_size == 0)
				{
					if (promptGroup.Count > 0)
					{
						promptGroups[starto] = promptGroup;
						promptGroup = new List<object>();
						starto++;
					}
					promptGroup.Add(prompt);
				}
				else promptGroup.Add(prompt);
			}
			Console.WriteLine("all have been added to groups");
			var mergedPrompts = new List<object>();
			for (int iterator = 0; iterator < promptGroups.Count; iterator++)
			{
				bool fail = false;
				var promgroup = promptGroups[iterator];
				if (promgroup is dynamic || promgroup is int)
				{
					Console.WriteLine("processing object. adding single item to list.");
					fail = true;
				}
				else
				{
					List<string> noncomplist = new List<string> { "promgroup", "negative_prompt", "all_prompts", "seed", "subseed" };
					var cprompt = promgroup as List<dynamic>;
					var promptAttr = cprompt[0];
					batchsize = promptAttr.batch_size;
					Console.WriteLine($"Merging prompts {iterator * batchsize} - {iterator * batchsize + batchsize} out of {promptGroups.Count * batchsize}");
					var complist = new List<List<PropertyInfo>>();
					for (int iter = 0; iter == promptGroup.Count; iter++)
					{
						var promp1 = promptGroup[iter];
						var l1 = promp1.GetType().GetProperties().ToList();
						complist.Add(l1);
						if (l1 != null && iter > 0 && l1.Count == complist[iter - 1].Count)
						{
							for (int iter2 = 0; iter2 > l1.Count; iter2++)
							{
								PropertyInfo p = l1[iter2];
								if (!noncomplist.Contains(p.Name))
								{
									Console.WriteLine(p.Name);
									if (p.GetValue(promp1) != complist[iter - 1][iter2].GetValue(promptGroup[iter - 1]))
									{
										fail = true;
										Console.WriteLine($"{p.Name} is not the same. promgroup can not be merged");
									}
								}
							}
						}
						else fail = true;
					}
				}

				if (!fail)
				{
					var cprompt = promgroup as List<dynamic>;
					var promptAttr = cprompt[0];
					dynamic mergedPrompt = promptAttr.MemberwiseClone();
					mergedPrompt.prompt = string.Join(",", cprompt.Select(p => p.prompt));
					mergedPrompt.negative_prompt = string.Join(",", cprompt.Select(p => p.negative_prompt));
					mergedPrompt.seed = string.Join(",", cprompt.Select(p => p.seed));
					mergedPrompt.subseed = string.Join(",", cprompt.Select(p => p.subseed));
					mergedPrompt.batchsize = cprompt.Count;
					string mergedPath = string.Join(",", appliedSets.Select(p => cprompt));
					foreach (var prompt in cprompt)
					{
						var setup2 = this.appliedSets.ContainsKey(prompt) ? this.appliedSets[prompt] : new List<AxisValue>();
						List<string> mergedFilePaths = new List<string>();
						foreach (AxisValue set in appliedSets[prompt])
						{
							mergedFilePaths.Add(set.Filepath);
						}

						if (this.appliedSets.ContainsKey(prompt) && this.appliedSets[prompt] == this.appliedSets[mergedPrompt]) continue;

						this.appliedSets[mergedPrompt].AddRange(this.appliedSets.ContainsKey(prompt) ? this.appliedSets[prompt] : new List<AxisValue>());
					}

					mergedPrompt.batch_size = cprompt.Count;
					mergedPrompts.Add(mergedPrompt);
				}
				else if (promgroup is not List<dynamic>)
				{
					dynamic promptAttr = promgroup;
					promptAttr.batch_size = 1;
					mergedPrompts.Add(promptAttr);
				}
				else if (promgroup is int)
				{
					continue;
				}
				else
				{
					foreach (var promp in promgroup as List<dynamic>)
					{
						promp.batch_size = 1;
					}
					mergedPrompts.AddRange(promgroup as List<dynamic>);
				}

				Console.WriteLine($"there are {mergedPrompts.Count} generations after merging");
			}
			return mergedPrompts;
		}
	}
	internal class SingleGridCall
	{
		internal List<AxisValue> values;
		internal AxisValue value;
		internal bool skip;
		internal Dictionary<string, object> parameters;
		internal sdinfin sdfin;

		internal SingleGridCall(List<AxisValue> values)
		{
			foreach (var value in values)
			{
				if (value.Skip)
				{
					this.skip = true;
				}
			}
		}

		internal void flattenParams(GridFileHelper grid)
		{
			var parameters = grid.parameters.ToDictionary(entry => entry.Key, entry => entry.Value) ?? new Dictionary<string, object>();
			foreach (var val in values)
			{
				for (int i = 0; i < val.Parameters.Count; i++)
				{
					KeyValuePair<string, string> param = val.Parameters.ElementAt(i);
					if (sdfin.core.GridCallParamAddHook(param.Key, param.Value))
						this.parameters[param.Key] = param.Value;
					else if (sdfin.core.GridCallParamAddHookneg(param.Key, param.Value))
						this.parameters[param.Key] = param.Value;
				}
			}
		}

		public void applyTo(dynamic p, bool dry)
		{
			foreach (var val in parameters)
			{
				string modename = GridGenCore.CleanName(val.Key);
				GridSettingMode mode = sdfin.validModes[modename];
				if (!dry || mode.dry)
					if (modename == "model") sdfin.core.modelchange[p] = val;
					else
						mode.apply(p, val.Value);
			}
		}
	}
	public class Axis
	{
		internal List<AxisValue> values;
		public string id;
		public string title;
		public object defaultVal;
		public string description;
		GridFileHelper grid;

		internal Axis(GridFileHelper grid, string id, object obj)
		{
			this.grid = grid;
			values = new List<AxisValue>();
			this.id = GridGenCore.cleanID(id);
			//TODO: what is this?
			/*
        if any(x.id == self.id for x in grid.axes):
            self.id += f"__{len(grid.axes)}"*/
			if (obj is string)
			{
				title = this.id;
				defaultVal = null;
				description = "";
				buildFromListStr(grid, obj as string);
			}
			else
			{
				var obj2 = obj as Dictionary<string, object>;
				title = grid.procVariables(obj2["Title"] as string);
				defaultVal = grid.procVariables(obj2["default"] as string);
				var valuesObj = obj2["values"];
				if (title == null || title == "")
				{
					throw new Exception("missing Title");
				}
				if (valuesObj == null)
				{
					throw new Exception("missing values");
				}
				else if (valuesObj is string)
				{
					buildFromListStr(grid, valuesObj as string);
				}
				else
				{
					foreach (var val in valuesObj as Dictionary<string, object>)
					{
						try
						{
							values.Append(new AxisValue(this, grid, val.Key, val.Value));
						}
						catch
						{
							throw new Exception($"invalid. {val.Key} errored");
						}
					}
				}
			}
		}

		internal void buildFromListStr(GridFileHelper grid, string listStr)
		{
			bool isPipeSplit = listStr.Contains("||");
			List<string> valuesList = listStr.Split(isPipeSplit ? new char[] { '|', '|' } : new char[] { ',' }).ToList();

			var mode = grid.parent.validModes[GridGenCore.CleanName(id as string)];
			if (mode.type == "integer")
				valuesList = GridGenCore.expandNumericListRanges(valuesList, typeof(int));
			else if (mode.type == "decimal")
				valuesList = GridGenCore.expandNumericListRanges(valuesList, typeof(double));
			int index = 0;
			for (int i = 0; i < valuesList.Count; i++)
			{
				var val = valuesList[i];
				try
				{
					val = val.Trim();
					index++;
					if (isPipeSplit && val == "" && index == valuesList.Count) continue;
					this.values.Add(new AxisValue(this, grid, index.ToString(), $"{id}={val}"));
				}
				catch
				{
					throw new Exception($"value errored: {val}");
				}
			}
		}
	}
	internal class AxisValue
	{
		public string Key;
		public string Title;
		public string Description;
		internal string Param;
		public bool Skip;
		public bool Show;
		public Dictionary<string, string> Parameters;
		internal string Filepath;
		internal object Data;
		internal Axis axis;

		public AxisValue(Axis axis, GridFileHelper grid, string key, dynamic val)
		{
			this.axis = axis;
			this.Key = GridGenCore.cleanID(key.ToString());
			bool foundKey = false;
			foreach (var axisValue in axis.values)
			{
				if (axisValue.Key == this.Key)
				{
					this.Key += $"__{axis.values.Count}";
					foundKey = true;
					break;
				}
			}
			Parameters = new Dictionary<string, string>();
			if (val is string)
			{
				string[] halves = val.Split('=', 2);
				if (halves.Length != 2)
				{
					throw new System.Exception($"Invalid value '{key}': '{val}': not expected format");
				}
				halves[0] = grid.procVariables(halves[0]);
				halves[1] = grid.procVariables(halves[1]);
				halves[1] = (string)grid.validateSingleParam(halves[0], halves[1]);
				Title = halves[1];
				Parameters = new Dictionary<string, string>
				{
					{ GridGenCore.CleanName(halves[0]), halves[1] }
				};
				Description = null;
				Skip = false;
				Show = true;
			}
			else
			{
				Title = grid.procVariables(val.title);
				Description = grid.procVariables(val.description);
				Skip = grid.procVariables(val.skip).ToString().ToLower() == "true";
				Parameters = GridGenCore.fixDict(val.parameters);
				Show = grid.procVariables(val.show).ToString().ToLower() != "false";
				if (Title == null || Parameters == null)
				{
					throw new System.Exception($"Invalid value '{key}': '{val}': missing Title or Parameters");
				}
				grid.validateParams(Parameters);
			}
		}

		public override string ToString()
		{
			return $"(Title={Title}, Description={Description}, Parameters={Parameters})";
		}

		internal void applyTo(dynamic processor, bool dry)
		{
			throw new NotImplementedException();
		}
	}
}