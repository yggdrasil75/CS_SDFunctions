using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Linq;
using static System.Net.Mime.MediaTypeNames;

namespace SDFunctions
{
	public class StableDiffusionProcessing
	{
		public string outpath_samples;
		public string outpath_grids;
		public string prompt;
		public string prompt_for_display;
		public List<object> styles;
		public int seed;
		public int subseed;
		public float subseed_strength;
		public int seed_resize_from_h;
		public int seed_resize_from_w;
		public string sampler_name;
		public int batch_size;
		public int n_iter;
		public int steps;
		public float cfg_scale;
		public int width;
		public int height;
		public bool restore_faces;
		public bool tiling;
		public bool do_not_save_samples;
		public bool do_not_save_grid;
		public Dictionary<string, object> extra_generation_params;
		public object overlay_images;
		public int eta;
		public bool do_not_reload_embeddings;
		public object paste_to;
		public object color_corrections;
		public float denoising_strength;
		public object sampler_noise_scheduler_override;
		public object ddim_discretize;
		public object s_churn;
		public float s_tmin;
		public float s_tmax;
		public object s_noise;
		public object override_settings;
		public bool is_using_inpaint_conditioning;
		public bool disable_extra_networks;
		public object scripts;
		public object script_args;
		public List<string> all_prompts;
		public List<string> all_negative_prompts;
		public List<int> all_seeds;
		public List<int> all_subseeds;
		public int iteration;
		internal Random random = new();

		public int get_fixed_seed()
		{
			if (seed == null || seed == -1)
			{
				seed = random.Next(294967294);
			}
			return seed;
		}

		public StableDiffusionProcessing(string outpath_samples, string outpath_grids, string prompt, List<object> styles, int seed, int subseed, float subseed_strength, int seed_resize_from_h, int seed_resize_from_w, string sampler_name, int batch_size, int n_iter, int steps, float cfg_scale, int width, int height, bool restore_faces, bool tiling, bool do_not_save_samples, bool do_not_save_grid, Dictionary<string, object> extra_generation_params, object overlay_images, int eta, bool do_not_reload_embeddings, object paste_to, object color_corrections, float denoising_strength, object sampler_noise_scheduler_override, object ddim_discretize, object churn, float tmin, float tmax, object noise, object override_settings, bool is_using_inpaint_conditioning, bool disable_extra_networks, object scripts, object script_args, List<string> all_prompts, List<string> all_negative_prompts, List<int> all_seeds, List<int> all_subseeds, int iteration)
		{
			this.outpath_samples = outpath_samples ?? throw new ArgumentNullException(nameof(outpath_samples));
			Console.WriteLine(outpath_samples);
			this.outpath_grids = outpath_grids ?? throw new ArgumentNullException(nameof(outpath_grids));
			Console.WriteLine(outpath_grids);
			this.prompt = prompt ?? throw new ArgumentNullException(nameof(prompt));
			Console.WriteLine(prompt);
			this.styles = styles ?? throw new ArgumentNullException(nameof(styles));
			Console.WriteLine(styles);
			this.seed = seed;
			Console.WriteLine(seed);
			this.subseed = subseed;
			Console.WriteLine(subseed);
			this.subseed_strength = subseed_strength;
			Console.WriteLine(subseed_strength);
			this.seed_resize_from_h = seed_resize_from_h;
			Console.WriteLine(seed_resize_from_h);
			this.seed_resize_from_w = seed_resize_from_w;
			Console.WriteLine(seed_resize_from_w);
			this.sampler_name = sampler_name ?? throw new ArgumentNullException(nameof(sampler_name));
			this.batch_size = batch_size;
			this.n_iter = n_iter;
			this.steps = steps;
			this.cfg_scale = cfg_scale;
			this.width = width;
			this.height = height;
			this.restore_faces = restore_faces;
			this.tiling = tiling;
			this.do_not_save_samples = do_not_save_samples;
			this.do_not_save_grid = do_not_save_grid;
			this.extra_generation_params = extra_generation_params ?? throw new ArgumentNullException(nameof(extra_generation_params));
			this.overlay_images = overlay_images ?? throw new ArgumentNullException(nameof(overlay_images));
			this.eta = eta;
			this.do_not_reload_embeddings = do_not_reload_embeddings;
			this.paste_to = paste_to ?? throw new ArgumentNullException(nameof(paste_to));
			this.color_corrections = color_corrections ?? throw new ArgumentNullException(nameof(color_corrections));
			this.denoising_strength = denoising_strength;
			this.sampler_noise_scheduler_override = sampler_noise_scheduler_override ?? throw new ArgumentNullException(nameof(sampler_noise_scheduler_override));
			this.ddim_discretize = ddim_discretize ?? throw new ArgumentNullException(nameof(ddim_discretize));
			s_churn = churn ?? throw new ArgumentNullException(nameof(churn));
			s_tmin = tmin;
			s_tmax = tmax;
			s_noise = noise ?? throw new ArgumentNullException(nameof(noise));
			this.override_settings = override_settings ?? throw new ArgumentNullException(nameof(override_settings));
			this.is_using_inpaint_conditioning = is_using_inpaint_conditioning;
			this.disable_extra_networks = disable_extra_networks;
			this.scripts = scripts ?? throw new ArgumentNullException(nameof(scripts));
			this.script_args = script_args ?? throw new ArgumentNullException(nameof(script_args));
			this.all_prompts = all_prompts ?? throw new ArgumentNullException(nameof(all_prompts));
			this.all_negative_prompts = all_negative_prompts ?? throw new ArgumentNullException(nameof(all_negative_prompts));
			this.all_seeds = all_seeds ?? throw new ArgumentNullException(nameof(all_seeds));
			this.all_subseeds = all_subseeds ?? throw new ArgumentNullException(nameof(all_subseeds));
			this.iteration = iteration;
		}
	}

	public static class CommonSDFuncs
	{

	}
}