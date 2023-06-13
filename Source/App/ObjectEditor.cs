using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.Threading;
using System.Diagnostics;

namespace Object_tool
{
	public partial class Object_Editor : Form
	{
		// File sytem
		EditorSettings pSettings = null;
		FolderSelectDialog SaveSklDialog = new FolderSelectDialog();
		FolderSelectDialog OpenBatchOutDialog = new FolderSelectDialog();
        List<string[]> batch_files = new List<string[]>();
		List<string> batch_source = new List<string>();
		public string SCRIPT_FOLDER = "";
		public Object m_Object = new Object();
		public List<string> SklsToLoad = new List<string>();

		// Input
		public bool bKeyIsDown = false;
		public Size CurrentSize = new Size();
		public Size BoneSize = new Size();
		public bool dbg_window = false;

		// Other
		public int cpp_mode = 0;
		public string[] game_materials = { };
		public Thread SdkThread = null;
		public Thread ViewerThread = null;
		public bool NORMALS_DEFAULT = true;
		List<Process> EditorProcess = new List<Process>();
		Process ViewerProcess = new Process();
		public bool ViewerWorking = false;
		public List<bool> EditorWorking = new List<bool>();
		public List<bool> EditorKilled = new List<bool>();

		// Program data
		public float lod_quality = 0.5f;
		public int lod_flags = 0;
		public bool IsOgfMode = false;
		public string script = "null";
		public double dLastCopmileTime = 0.0;

		// Settings
		public bool USE_OLD_BONES = true;

		const int GWL_STYLE = -16;
		const int WS_CAPTION = 0x00C00000;
		const int WS_THICKFRAME = 0x00040000;
		const int SWP_NOACTIVATE = 0x0010;
		const int SWP_NOZORDER = 0x0004;

		[DllImport("user32.dll")]
		private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

		[DllImport("user32.dll", SetLastError = true)]
		private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

		[DllImport("user32")]
		private static extern IntPtr SetParent(IntPtr hWnd, IntPtr hWndParent);

		[DllImport("user32")]
		private static extern bool SetWindowPos(
		  IntPtr hWnd,
		  IntPtr hWndInsertAfter,
		  int X,
		  int Y,
		  int cx,
		  int cy,
		  int uFlags);

		public Object_Editor()
		{
			InitializeComponent();

			Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("en-US");

			EditorProcess.Add(new Process());
			EditorProcess.Add(new Process());
			EditorWorking.Add(false);
			EditorWorking.Add(false);
			EditorKilled.Add(false);
			EditorKilled.Add(false);

			EditorProcess[0].OutputDataReceived += LogOutputHandler;
            EditorProcess[0].StartInfo.UseShellExecute = false;
			EditorProcess[1].OutputDataReceived += LogOutputHandler;
			EditorProcess[1].StartInfo.UseShellExecute = false;

			InitUI();
			InitSettings();
			InitScripts();
		}

		private void EditorLoaded(object sender, EventArgs e)
		{
			InitArgs();
		}

		public void OpenFile(string filename)
		{
			m_Object = new Object();
			m_Object.Init(filename, TempFolder());
			m_Object.LoadData();
			m_Object.LoadMotions();
			AfterLoad();

			InitDialogs();
			UpdateUI();
			InitSurfaceUI();
			InitBoneUI();

			IndexChanged(null, null);

			if (!IsOgfMode)
			{
				MotionRefsTextChanged(MotionRefsBox, null);
				UserDataTextChanged(UserDataTextBox, null);
			}

			bool ForceViewPort = true;
			pSettings.LoadState("ForceViewport", ref ForceViewPort, true);

			if (ForceViewPort)
				ViewtoolStripMenuItem_Click(null, null);
			else if (ViewerWorking)
            {
				ViewerProcess.Kill();
				ViewerProcess.Close();
				ViewerWorking = false;
				CreateViewPort.Visible = true;
				CreateViewPort.Enabled = true;
			}
		}

		public void AfterLoad()
        {
			UserDataTextBox.Text = m_Object.userdata;
			LodTextBox.Text = m_Object.lod;
			ObjectScaleTextBox.Text = ((decimal)m_Object.scale).ToString();
			ScaleCenterOfMassCheckBox.Checked = m_Object.scale_center_of_mass;
			MotionRefsBox.Lines = m_Object.motion_refs.ToArray();
			ModelTypeCBox.SelectedIndex = (m_Object.flags & (uint)Object.ObjectFlags.eoDynamic) == (uint)Object.ObjectFlags.eoDynamic ? 0 : 1;
			ModelTypeCBox.Enabled = m_Object.bones.Count > 0;

			bool AutoSmoothEnabled = false;
			bool SmoothChanged = false;
			pSettings.LoadState("AutoSmooth", ref AutoSmoothEnabled, true);

			if (m_Object.motion_refs.Count > 0)
            {
				if (!AutoSmoothEnabled)
				{
					SmoothCoP.Checked = m_Object.cop_refs;
					SmoothSoC.Checked = !m_Object.cop_refs;
					SmoothChanged = true;
				}
			}

			UseSplitNormals.Enabled = false;
			UseSplitNormals.Checked = false;
			normalsToolStripMenuItem.Enabled = false;

			if (m_Object.has_normals)
			{
				UseSplitNormals.Enabled = true;
				UseSplitNormals.Checked = NORMALS_DEFAULT && !AutoSmoothEnabled;
				normalsToolStripMenuItem.Enabled = true;
				SmoothChanged = NORMALS_DEFAULT;
			}

			if (!SmoothChanged && !AutoSmoothEnabled)
			{
				pSettings.Load(SmoothSoC);
				pSettings.Load(SmoothCoP, true);
			}

			AfterLoadMotions();
		}

		public void AfterLoadMotions()
        {
			MotionTextBox.Clear();
			MotionTextBox.Text = $"Motions count: 0";
			MotionRefsTextChanged(MotionRefsBox, null);

			MotionFlagsGroupBox.Enabled = m_Object.motions.Count > 0;
			if (IsOgfMode)
				ScaleGroupBox.Enabled = m_Object.motions.Count > 0;

			if (m_Object.motions.Count > 0)
            {
				MotionTextBox.Clear();
				MotionTextBox.Text = $"Motions count: {m_Object.motions.Count}\n";
			}

			for (int i = 0; i < m_Object.motions.Count; i++)
            {
				MotionTextBox.Text += $"\n{i + 1}. {m_Object.motions[i].name}";
			}
        }

		public void AfterCopy()
		{
			UserDataTextBox.Text = m_Object.userdata;
			LodTextBox.Text = m_Object.lod;
			MotionRefsBox.Lines = m_Object.motion_refs.ToArray();

			for (int i = 0; i < m_Object.surfaces.Count; i++)
            {
				(SurfacesPage.Controls[i].Controls[0] as CheckBox).Checked = (m_Object.surfaces[i].flags == 1);
				SurfacesPage.Controls[i].Controls[2].Text = m_Object.surfaces[i].texture;
				SurfacesPage.Controls[i].Controls[4].Text = m_Object.surfaces[i].shader;
			}

			for (int i = 0; i < m_Object.bones.Count; i++)
			{
				(BonesPage.Controls[i].Controls[4] as ComboBox).SelectedIndex = m_Object.bones[i].shape_type;

				(BonesPage.Controls[i].Controls[0] as CheckBox).Checked = (m_Object.bones[i].shape_flags & (1 << 0)) == (1 << 0);
				(BonesPage.Controls[i].Controls[1] as CheckBox).Checked = (m_Object.bones[i].shape_flags & (1 << 1)) == (1 << 1);
				(BonesPage.Controls[i].Controls[2] as CheckBox).Checked = (m_Object.bones[i].shape_flags & (1 << 2)) == (1 << 2);
				(BonesPage.Controls[i].Controls[3] as CheckBox).Checked = (m_Object.bones[i].shape_flags & (1 << 3)) == (1 << 3);

				BonesPage.Controls[i].Controls[7].Text = m_Object.bones[i].material;
				BonesPage.Controls[i].Controls[9].Text = m_Object.bones[i].mass.ToString();
			}
		}

		private string[] GameMtlParser(string filename)
        {
			List<string> materials = new List<string>();

			if (File.Exists(filename))
			{
				var xr_loader = new XRayLoader();
				using (var r = new BinaryReader(new FileStream(filename, FileMode.Open)))
				{
					xr_loader.SetStream(r.BaseStream);
					xr_loader.SetData(xr_loader.find_and_return_chunk_in_chunk((int)MTL.GAMEMTLS_CHUNK_MTLS, false, true));

					int id = 0;
					uint size;

					while (true)
					{
						if (!xr_loader.find_chunk(id)) break;

						Stream temp = xr_loader.reader.BaseStream;

						if (!xr_loader.SetData(xr_loader.find_and_return_chunk_in_chunk(id, false, true))) break;

						size = xr_loader.find_chunkSize((int)MTL.GAMEMTL_CHUNK_MAIN);
						if (size == 0) break;
						xr_loader.ReadBytes(4);
						materials.Add(xr_loader.read_stringZ());

						id++;
						xr_loader.SetStream(temp);
					}
				}
			}
			string[] ret = materials.ToArray();
			Array.Sort(ret);
			return ret;
        }

		public void ReloadGameMtl(string filename)
		{
			game_materials = GameMtlParser(filename);
			
			BonesPage.Controls.Clear();
			for (int i = 0; i < m_Object.bones.Count; i++)
				CreateBoneGroupBox(i, m_Object.bones[i]);
		}

		private void FastSaveObject(string filename)
		{
			if (m_Object.TEMP_FILE_NAME != "")
			{
				StartEditor(true, EditorMode.SaveObject, m_Object.TEMP_FILE_NAME);
			
				if (!EditorKilled[0])
				{
					File.Copy(m_Object.TEMP_FILE_NAME, filename, true);
					AutoClosingMessageBox.Show($"Object successfully saved.{GetTime()}", "", 1000, MessageBoxIcon.Information);
				}
			}
		}

        private void CheckTempFileExist()
        {
			if (!File.Exists(m_Object.TEMP_FILE_NAME) && File.Exists(m_Object.FILE_NAME))
			{
				if (!Directory.Exists(TempFolder()))
					Directory.CreateDirectory(TempFolder());

				File.Copy(m_Object.FILE_NAME, TempFolder() + $"\\{m_Object.NAME}", true);
			}
		}

		private string GetRetCode(int code)
		{
			string ret = "";
			if (debugToolStripMenuItem.Visible)
				ret += "\nExit code: " + code.ToString();
			return ret;
		}

		private string GetTime()
		{
			int minutes = 0;
			int hours = 0;

			while (dLastCopmileTime > 60.0)
            {
				minutes++;
				dLastCopmileTime -= 60.0;
			}

			while (minutes > 60)
			{
				hours++;
				minutes -= 60;
			}

			if (minutes == 0)
				return $"Time: {dLastCopmileTime} sec.";
			else if (hours == 0)
				return $"Time: {minutes} min {dLastCopmileTime} sec.";
			else
				return $"Time: {hours} hour {minutes} min {dLastCopmileTime} sec.";
		}

		private int GetErrorTime()
		{
			if (debugToolStripMenuItem.Visible)
				return 15000;
			return 1000;
		}

		private string[] DirSearch(string sDir)
		{
			List<string> files = new List<string>();
			try
			{
				foreach (string f in Directory.GetFiles(sDir))
				{
					files.Add(f);
				}
				foreach (string d in Directory.GetDirectories(sDir))
				{
					files.AddRange(DirSearch(d));
				}
			}
			catch (System.Exception excpt)
			{
				MessageBox.Show(excpt.Message);
			}

			return files.ToArray();
		}

		private string[] SortFormat(string[] source, string format)
		{
			List<string> files = new List<string>();

			foreach (string s in source)
            {
				if (Path.GetExtension(s) == "." + format)
					files.Add(s);
			}

			return files.ToArray();
		}

		private int BitSet(int flags, int mask, bool bvalue)
		{
			if (bvalue)
				return flags |= mask;
			else
				return flags &= ~mask;
		}

		private bool IsTextCorrect(string text)
		{
			foreach (char ch in text)
			{
				if (ch > 0x1F && ch != 0x20)
					return true;
			}
			return false;
		}

		private string GetCorrectString(string text)
		{
			string ret_text = "", symbols = "";
			bool started = false;
			foreach (char ch in text)
			{
				if (started)
				{
					if (ch <= 0x1F || ch == 0x20)
						symbols += ch;
					else
					{
						ret_text += symbols + ch;
						symbols = "";
					}
				}
				else if (ch > 0x1F && ch != 0x20)
				{
					started = true;
					ret_text += ch;
				}
			}
			return ret_text;
		}

		public void Msg(string text)
		{
			MessageBox.Show(text);
		}

		public string AppPath()
		{
			return Application.ExecutablePath.Substring(0, Application.ExecutablePath.LastIndexOf('\\'));
		}

		public string TempFolder()
		{
			return Application.ExecutablePath.Substring(0, Application.ExecutablePath.LastIndexOf('\\')) + "\\temp";
		}

		public string FileFolder()
		{
			if (m_Object != null)
				return m_Object.FILE_NAME.Substring(0, m_Object.FILE_NAME.LastIndexOf('\\'));
			else
				return "";
		}

		private void modelExportToolStripMenuItem_Click(object sender, EventArgs e)
        {
			MessageBox.Show(
			"1. Make progressive meshes - создает прогрессивные меши при экспорте OGF. Это динамическая детализация модели (lod'ы), используется для оптимизации мировых объектов.\n" +
			"2. Make stripify meshes - оптимизация vertex'ов и face'ов у мешей которая портила сетку полигонов, раньше стояла по дефолту в SDK и использовалась для оптимизации мешей под старый DirectX и видеокарты. Можно включать для оптимизации мировых моделей.\n" +
			"3. Optimize surfaces - объединяет меши с одинаковыми текстурами и шейдерами в один.\n" +
			"4. HQ Geometry+ - компилятор не будет удалять похожие vertex'ы и face'ы, поддержка более плотной сетки полигонов.\n" +
			"5. SoC bone export - при экспорте динамического OGF, на полигон будут влиять максимум 2 кости. При отключении будет включено CoP влияние в 4 кости (не поддерживается в SoC).\n" +
			"6. Smooth Type определяет тип сглаживания при экспорте моделей.\n1) SoC: #1\n2) CS\\CoP: #2\n3) Normals: использует оригинальные Split нормали.\n4) Auto: программа будет автоматически определять тип сглаживания, первыми в приоритете стоят нормали. Если их нет то начнется выбор типа сглаживания.\nПравильно определяет 95% моделей, чаще всего неправильно определяет хайполи модели."
			, "Help", MessageBoxButtons.OK, MessageBoxIcon.Information);
		}

        private void motionExportToolStripMenuItem_Click(object sender, EventArgs e)
        {
			MessageBox.Show(
			"1. 8 bit - SoC Формат, плохое качество.\n2. 16 bit - CoP Формат, хорошее качество.\n" + (AnimsNoCompress.Visible ? "3. No compress - Новый формат без сжатия, лучшее качество.\n" : "") +
			(AnimsNoCompress.Visible ? "4." : "3.") + " Use build-in motions - При активации программа будет использовать загруженные анимации вместо моушн референсов, если анимаций нет то будут использованы референсы, если таковы есть. При деактивации загруженные анимации будут игнорированы. Влияет на всё кроме сохранения object."
			, "Help", MessageBoxButtons.OK, MessageBoxIcon.Information);
		}

        private void scaleToolStripMenuItem_Click(object sender, EventArgs e)
        {
			MessageBox.Show(
			"1. Object scale - изменяет размер экспортированных OGF, OMF и Obj.\n" +
			"2. Scale center of mass - при активации центр массы кости будет пересчитан под новый размер."
			, "Help", MessageBoxButtons.OK, MessageBoxIcon.Information);
		}

        private void bonesToolStripMenuItem2_Click(object sender, EventArgs e)
        {
			MessageBox.Show(
			"1. No Pickable - Ray Query лучи, хит wallmark'и будут пропускать данный элемент.\n" +
			"2. Remove After Break - при активации у всех костей, после спавна объекта начнется таймер \"remove_time\" из конфига, при истечении которого объект удалится.\n" +
			"3. No Physics - движок игнорирует физику шейпа.\n" +
			"4. No Fog Collider - Volumetric fog будет игнорировать данный элемент.\n" +
			"5. Material - определяет материал кости при ударах\\столкновениях итд. который влияет на звук и партиклы.\n" +
			"6. Mass - масса кости."
			, "Help", MessageBoxButtons.OK, MessageBoxIcon.Information);
		}

        private void surfacesToolStripMenuItem_Click(object sender, EventArgs e)
        {
			MessageBox.Show(
			"1. Surface->2 Sided - после экспорта OGF, Obj модель будет отрисовываться с наружной и внутренней стороны, в 2 раза увеличивает колличество полигонов у модели."
			, "Help", MessageBoxButtons.OK, MessageBoxIcon.Information);
		}

        private void otherToolStripMenuItem_Click(object sender, EventArgs e)
        {
			MessageBox.Show(
			"Для создания lod модели нужно нажать Tools->Generate lod, появится окно с настройкой детализации лода, после нажатия кнопки Append сгенерируется lod модель. Если она была сохранена в геймдату игры, то референс лода автоматически пропишестя в текщую модель, иначе его нужно будет прописывать вручную."
			, "Help", MessageBoxButtons.OK, MessageBoxIcon.Information);
		}

        private void hotkeysToolStripMenuItem_Click(object sender, EventArgs e)
        {
			MessageBox.Show(
			"F3 - Экспорт\nF4 - Загрузка\nF5, Ctrl+S - Быстрое сохранение .object\nF6 - Сохранение\nCtrl+Del - Закрытие текущего процесса"
			, "Help", MessageBoxButtons.OK, MessageBoxIcon.Information);
		}

        private void ltxToolStripMenuItem_Click(object sender, EventArgs e)
        {
			MessageBox.Show("Batch Converter создан для массового экспорта моделей и анимаций.\nВ данной программе имеется 3 режима: From Ltx, From File Dialog и From Folder Dialog.\n\n" +
			"From Ltx - Пример ltx конфига:\n\n" +
			"[ogf] ; секция из которой будут экспортироватсья ogf модели\n" +
			"test.object = test.ogf ; test.object из папки где находится ltx будет экспортирован в test.ogf\n" +
			"test2 = test3 ; можно указывать без форматов файлов, все равно будет работать\n" +
			"test\\test3 = test\\test3 ; так же можно прописывать папки\n\n" +
			"[omf] ; секция из которой будут экспортироваться omf анимации\n" +
			"test.object = test.omf ; встроенные анимации из test.object будут экспортированны в test.omf\n" +
			"test\\test = test\\test ; все так же можно прописывать без указания формата и в папках\n\n" +
			"[skls_skl] ; новая секция которая есть только в Object Editor, подгружает анимации в модели перед экспортом\n" +
			"test.object = test1.skl, test\\test2.skls, test3.skl ; все анимации из списка будут загружены в test.object перед экспортом в ogf и omf\n" +
			"test = test1, test\\test2, test3 ; указание без форматов и расположение в папках так же работает, программа будет искать анимации в skls и skl формате"
		, "Help", MessageBoxButtons.OK, MessageBoxIcon.Information);
		}

        private void dialogsToolStripMenuItem_Click(object sender, EventArgs e)
        {
			MessageBox.Show("Batch Converter создан для массового экспорта моделей и анимаций.\nВ данной программе имеется 3 режима: From Ltx, From File Dialog и From Folder Dialog.\n\n" +
			"From Dialog - принцип работы:\n" +
			"Object будет экспортирован в выбранный вами формат.\nПри экспорте программа будет искать skls анимации лежащие рядом с ним с таким же названием что и у object, и при нахождении загрузит их в модель перед экспортом.\n\n" +
			"При выборе From File Dialog откроется окно выбора файлов, после выбора необходимых моделей откроется окно выбора папки куда будут сохранены все выбранные модели.\n" +
			"При выборе From Folder Dialog откроется окно выбора папок, после выбора папок с моделями откроется окно выбора папки куда будут сохранены все выбранные папки с моделями внутри."
			, "Help", MessageBoxButtons.OK, MessageBoxIcon.Information);
		}

        private void settingsToolStripMenuItem1_Click(object sender, EventArgs e)
        {
			MessageBox.Show(
			"В меню настроек можно установить дефолтные значения параметров при первом запуске программы, а так же активировать дополнительные функции.\n\n" +
            "Presets - выставит вам параметры для комфортной работы с выбранной игрой из трилогии.\n" +
            "Use split normals - будет выставлять Smooth Type на Normals при наличии этих нормалей. При деактивации активировать нормали нужно будет вручную.\n\n" +
            "Настройки программы:\n" +
			"1. Use No Compress motions - активирует новый параметр компрессии анимаций, выбрав который анимации будут экспортированы без сжатия. Требует наличие коммита из STCoP WP\n" +
			"2. Program debugging - активирует вкладку с кнопками для отладки.\n" +
			"3. Force viewport load - вьюпорт автоматически загружается при каждой загрузке файла (замедляет загрузку).\n" +
			"4. FS path - при выборе fs.ltx программа автоматически пропишет все остальные пути к файлам геймдаты.\n" +
			"5. Textures path - папка текстур для вьюпорта.\n" +
			"6. Game Mtl path - при выборе gamemtl.xr во вкладке Bones можно будет выбрать и применить материал из gamemtl.\n" +
			"7. Image path - папка для сгенерированных скриншотов из вьюпорта.\n\n" +
			"Остальные параметры вы могли встречать в первых 3х пунктах в Help."
			, "Help", MessageBoxButtons.OK, MessageBoxIcon.Information);
		}

        private void shapesGenerateToolStripMenuItem_Click(object sender, EventArgs e)
        {
			MessageBox.Show(
			"Для создания коллизии с нуля нужно настроить Shape type параметры у каждой кости если таковы были не настроены (можно воспользоваться Tools->Shape Params) и далее нажать Tools->Generate Shapes.\nЕсли коллизия уже была сгенерирована, то Shape type можно менять без повторной генерации коллизии."
			, "Help", MessageBoxButtons.OK, MessageBoxIcon.Information);
		}

		private void viewToolStripMenuItem1_Click(object sender, EventArgs e)
		{
			MessageBox.Show(
				"Примечания:\n1. Вьюпорт отображает выбранное вами сглаживание во вкладке Flags, благодаря этому можно будет узнать какое сглаживание нужно использовать.\nHQ Geometry и HQ Geometry+ влияют на модель при просмотре.\n2. Для удобства нужно ознакомиться с возможностями через клавишу 'H' в окне вьюпорта.\n3. Если указать путь к текстурам в настройках программы (или указать путь к fs.ltx), то вьюпорт будет отображать текстуры.\n4. в Image path в настройках будут сохраняться сгенерированные скриншоты."
				, "Help", MessageBoxButtons.OK, MessageBoxIcon.Information);
		}



		// Events
		private void LogOutputHandler(object sendingProcess, DataReceivedEventArgs outLine)
		{
			if (outLine.Data != null)
			{
				this.Invoke((MethodInvoker)delegate () {
					LogTextBox.AppendText(outLine.Data + "\n");
					LogTextBox.SelectionStart = LogTextBox.Text.Length;
					LogTextBox.ScrollToCaret();
					ShowErrors(LogTextBox);
                });
			}
		}

        private void ShowErrors(RichTextBox richTextBox)
        {
            Color highlightColor = Color.Red;
            string[] lines = richTextBox.Lines;

            richTextBox.SelectionStart = 0;
            richTextBox.SelectionLength = richTextBox.TextLength;
            richTextBox.SelectionColor = richTextBox.ForeColor;

            foreach (string line in lines)
            {
                if (line.StartsWith("!"))
                {
                    int startIndex = richTextBox.Text.IndexOf(line);
                    richTextBox.SelectionStart = startIndex;
                    richTextBox.SelectionLength = line.Length;
                    richTextBox.SelectionColor = highlightColor;
                }
            }

            richTextBox.SelectionStart = 0;
            richTextBox.SelectionLength = 0;
        }

        private void objectInfoToolStripMenuItem_Click(object sender, EventArgs e)
		{
			long vertex_count = 0;
			int surface_count = m_Object.surfaces.Count;
			int joints_count = m_Object.bones.Count;

			long surfFacesFaces = 0;
			for (int i = 0; i < m_Object.meshes.Count; i++)
			{
				vertex_count += m_Object.meshes[i].vertex_count;

				for (int j = 0; j < m_Object.meshes[i].surfFaces.Count; j++)
				{
					Object.Surface surface = m_Object.GetSurfaceByName(m_Object.meshes[i].surfFaces[j].name);
					surfFacesFaces += m_Object.meshes[i].surfFaces[j].faces * (surface != null ? (surface.flags == 1 ? 2 : 1) : 1);
				}
			}

			MessageBox.Show($"Raw Vertex count: {vertex_count}\nFace count: {surfFacesFaces}\nSurface count: {surface_count}\nJoints count: {joints_count}", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
		}

		private void IndexChanged(object sender, EventArgs e)
		{
			if (TabControl.SelectedIndex < 0 || !FlagsGroupBox.Enabled || TabControl.Controls.Count < TabControl.SelectedIndex) return;

			switch (TabControl.Controls[TabControl.SelectedIndex].Name)
			{
				case "BonesPage":
					if (!USE_OLD_BONES)
						this.Size = BoneSize;
					break;
				case "SurfacesPage":
					if (!USE_OLD_BONES)
						this.Size = CurrentSize;
					break;
				default:
					if (!USE_OLD_BONES)
						this.Size = CurrentSize;
					break;
			}
		}

		private void SwitchShapeType(object sender, EventArgs e)
		{
			ToolStripItem Item = sender as ToolStripItem;
			int type = Convert.ToInt32(Item.Tag.ToString().Split('_')[1]);

			for (int i = 0; i < m_Object.bones.Count; i++)
			{
				m_Object.bones[i].shape_type = (ushort)type;
				(BonesPage.Controls[i].Controls[4] as ComboBox).SelectedIndex = type;
			}
		}

		private void importObjectParamsToolStripMenuItem_Click(object sender, EventArgs e)
		{
			OpenFileDialog Dialog = new OpenFileDialog();
			Dialog.Filter = "Object file|*.object";

			if (Dialog.ShowDialog() == DialogResult.OK)
			{
				m_Object.CopyParams(Dialog.FileName);
				AfterCopy();
			}
		}

		private void ClosingForm(object sender, FormClosingEventArgs e)
		{
			try
			{
				if (SdkThread != null && SdkThread.ThreadState != System.Threading.ThreadState.Stopped)
					SdkThread.Abort();

				if (ViewerWorking)
				{
					ViewerProcess.Kill();
					ViewerProcess.Close();
					ViewerWorking = false;
				}

				for (int i = 0; i < EditorProcess.Count; i++)
				{
					if (EditorWorking[i])
					{
						EditorKilled[i] = true;
						EditorProcess[i].Kill();
						EditorProcess[i].Close();
						EditorWorking[i] = false;
					}
				}

				if (Directory.Exists(TempFolder()))
					Directory.Delete(TempFolder(), true);

				pSettings.Save("FirstLoad", false);
			}
			catch (Exception) { }
		}

		private void ClosedForm(object sender, FormClosedEventArgs e)
		{
			try
			{
				if (SdkThread != null && SdkThread.ThreadState != System.Threading.ThreadState.Stopped)
					SdkThread.Abort();

				if (ViewerWorking)
				{
					ViewerProcess.Kill();
					ViewerProcess.Close();
					ViewerWorking = false;
				}

				for (int i = 0; i < EditorProcess.Count; i++)
				{
					if (EditorWorking[i])
					{
						EditorKilled[i] = true;
						EditorProcess[i].Kill();
						EditorProcess[i].Close();
						EditorWorking[i] = false;
					}
				}

				if (Directory.Exists(TempFolder()))
					Directory.Delete(TempFolder(), true);
			}
			catch (Exception) { }
		}

		private void FloatTextChanged(object sender, EventArgs e, ref float val)
		{
			if (bKeyIsDown)
			{
				TextBox curBox = sender as TextBox;

				if (curBox.Text.Length == 0)
					return;

				int len = curBox.Text.Contains(".") ? 9 : 8;

				while (curBox.Text.Length >= len)
				{
					if (curBox.SelectionStart < 1)
						curBox.SelectionStart = curBox.Text.Length;

					int tmp = curBox.SelectionStart;
					curBox.Text = curBox.Text.Remove(curBox.Text.Length - 1, 1);
					curBox.SelectionStart = tmp;
				}

				string number_mask = @"^-[0-9.]*$";
				int temp = curBox.SelectionStart;
				string mask = number_mask;
				Regex.Match(curBox.Text, mask);

				try
				{
					Convert.ToSingle(curBox.Text);
					val = Convert.ToSingle(curBox.Text);
				}
				catch (Exception)
				{
					curBox.Text = val.ToString();

					if (curBox.SelectionStart < 1)
						curBox.SelectionStart = curBox.Text.Length;

					curBox.SelectionStart = temp - 1;
				}
				bKeyIsDown = false;
			}
		}

		private void ScaleTextChanged(object sender, EventArgs e)
		{
			FloatTextChanged(sender, e, ref m_Object.scale);
		}

		private void MassTextChanged(object sender, EventArgs e)
		{
			TextBox curBox = sender as TextBox;
			int idx = Convert.ToInt32(curBox.Name.ToString().Split('_')[1]);
			FloatTextChanged(sender, e, ref m_Object.bones[idx].mass);
		}

		private void MaterialTextChanged(object sender, EventArgs e)
		{
			Control curBox = sender as Control;
			int idx = Convert.ToInt32(curBox.Name.ToString().Split('_')[1]);
			m_Object.bones[idx].material = GetCorrectString(curBox.Text);
		}

		private void TextBoxKeyDown(object sender, KeyEventArgs e)
		{
			bKeyIsDown = true;
		}

		private void TextBoxKeyPress(object sender, KeyPressEventArgs e)
		{
			TextBox textBox = sender as TextBox;
			if (!(Char.IsDigit(e.KeyChar)) && !((e.KeyChar == '.') && (textBox.Text.IndexOf(".") == -1) &&(textBox.Text.Length != 0)))
			{
				if (e.KeyChar != (char)Keys.Back) e.Handled = true;
			}
		}

		private void openLogToolStripMenuItem_Click(object sender, EventArgs e)
		{
			string log = AppPath() + "\\engine.log";
			if (File.Exists(log))
				System.Diagnostics.Process.Start("notepad.exe", log);
			else
				AutoClosingMessageBox.Show("Can't find log.", "", 1000, MessageBoxIcon.Error);
		}

		private void showWindowToolStripMenuItem_Click(object sender, EventArgs e)
		{
			dbg_window = !dbg_window;
			if (dbg_window)
				showWindowToolStripMenuItem.Text = "Hide debug window";
			else
				showWindowToolStripMenuItem.Text = "Show debug window";
		}

		private void enableAll2SidedToolStripMenuItem_Click(object sender, EventArgs e)
		{
			for (int i = 0; i < m_Object.surfaces.Count; i++)
			{
				m_Object.surfaces[i].flags = 1;
				(SurfacesPage.Controls[i].Controls[0] as CheckBox).Checked = true;
			}
		}

		private void disableAll2SidedToolStripMenuItem_Click(object sender, EventArgs e)
		{
			for (int i = 0; i < m_Object.surfaces.Count; i++)
			{
				m_Object.surfaces[i].flags = 0;
				(SurfacesPage.Controls[i].Controls[0] as CheckBox).Checked = true;
			}
		}

		private void TextBoxFilter(object sender, EventArgs e)
		{
			 TextBox curBox = sender as TextBox;
			 
			 string currentField = curBox.Name.ToString().Split('_')[0];
			 int idx = Convert.ToInt32(curBox.Name.ToString().Split('_')[1]);
			 
			 switch (currentField)
			 {
			 	case "TextureTextBox":
			 		m_Object.surfaces[idx].texture = curBox.Text;
			 		break;
			 	case "ShaderTextBox":
			 		m_Object.surfaces[idx].shader = curBox.Text;
			 		break;
			 }
		}

		private void ScriptClicked(object sender, EventArgs e)
		{
			ToolStripMenuItem item = sender as ToolStripMenuItem;
			script = item.Tag.ToString();
			loadScriptToolStripMenuItem.Text = "Delete Script";

			for (int i = 0; i < loadScriptToolStripMenuItem.DropDownItems.Count; i++)
				loadScriptToolStripMenuItem.DropDownItems[i].Visible = false;
		}

		private void LoadScriptClicked(object sender, EventArgs e)
		{
			if (script != "null")
			{
				script = "null";
				loadScriptToolStripMenuItem.Text = "Load Script";

				for (int i = 0; i < loadScriptToolStripMenuItem.DropDownItems.Count; i++)
					loadScriptToolStripMenuItem.DropDownItems[i].Visible = true;
			}
		}

		private void CheckBoxCheckedChanged(object sender, EventArgs e)
		{
			CheckBox curBox = sender as CheckBox;

			string currentField = curBox.Name.ToString().Split('_')[0];
			int idx = Convert.ToInt32(curBox.Name.ToString().Split('_')[1]);

			switch (currentField)
			{
				case "chbx2sided":
					m_Object.surfaces[idx].flags = (uint)(curBox.Checked ? 1 : 0);
					Object.Mesh.SurfFace surfFace = m_Object.GetSurfFaceByName(m_Object.surfaces[idx].name);
					SurfacesPage.Controls[idx].Controls[5].Text = "Face count: " + (surfFace != null ? (surfFace.faces * ((m_Object.surfaces[idx].flags == 1) ? 2 : 1)).ToString() : "null");
					break;
				case "chbxNoPickable":
					m_Object.bones[idx].shape_flags = (ushort)BitSet(m_Object.bones[idx].shape_flags, (1 << 0), curBox.Checked);
					break;
				case "chbxNoPhysics":
					m_Object.bones[idx].shape_flags = (ushort)BitSet(m_Object.bones[idx].shape_flags, (1 << 1), curBox.Checked);
					break;
				case "chbxRemoveAfterBreakCheckBoxBox":
					m_Object.bones[idx].shape_flags = (ushort)BitSet(m_Object.bones[idx].shape_flags, (1 << 2), curBox.Checked);
					break;
				case "chbxNoFogColliderCheckBox":
					m_Object.bones[idx].shape_flags = (ushort)BitSet(m_Object.bones[idx].shape_flags, (1 << 3), curBox.Checked);
					break;
			}
		}

		private void ComboBoxIndexChanged(object sender, EventArgs e)
		{
			ComboBox curBox = sender as ComboBox;

			string currentField = curBox.Name.ToString().Split('_')[0];
			int idx = Convert.ToInt32(curBox.Name.ToString().Split('_')[1]);

			switch (currentField)
			{
				case "cbxType":
					m_Object.bones[idx].shape_type = (ushort)curBox.SelectedIndex;
					break;
			}
		}

		private void EditorKeyDown(object sender, KeyEventArgs e)
		{
			if (e.Control && e.KeyCode == Keys.Delete)
			{
				try
				{
					if (EditorWorking[0])
					{
						EditorKilled[0] = true;
						EditorProcess[0].Kill();
						EditorProcess[0].Close();
						EditorWorking[0] = false;
					}
					AutoClosingMessageBox.Show("Process Closed!", "", 1000, MessageBoxIcon.Information);
				}
				catch (Exception)
				{
					AutoClosingMessageBox.Show("Process not valid!", "", 1000, MessageBoxIcon.Information);
				}
			}

			switch (e.KeyData)
			{
				case Keys.F3:
					fileToolStripMenuItem.ShowDropDown();
					exportToolStripMenuItem.ShowDropDown();
					break;
				case Keys.F4:
					fileToolStripMenuItem.ShowDropDown();
					loadToolStripMenuItem.ShowDropDown();
					break;
				case Keys.F5:
					if (!CheckThread()) return;

					SdkThread = new Thread(() => { FastSaveObject(m_Object.FILE_NAME); });
					SdkThread.Start();
					break;
				case Keys.F6:
					fileToolStripMenuItem.ShowDropDown();
					saveToolStripMenuItem.ShowDropDown();
					break;
			}
		}

		private void DragDropCallback(object sender, DragEventArgs e)
		{
			string[] fileList = (string[])e.Data.GetData(DataFormats.FileDrop, false);
			SklsToLoad.Clear();

			for (int i = 0; i < fileList.Count(); i++)
            {
				if (Path.GetExtension(fileList[i]) == ".object")
                {
					OpenFile(fileList[i]);
					break;
                }
				else if (Path.GetExtension(fileList[i]) == ".bones")
				{
					if (m_Object.bones.Count <= 0) return;
					if (!CheckThread()) break;

					SdkThread = new Thread(() => {
						int code = StartEditor(true, EditorMode.LoadBones, m_Object.TEMP_FILE_NAME, fileList[i]);
						if (!EditorKilled[0])
						{
							if (code == 0)
							{
								AutoClosingMessageBox.Show($"Bone data successfully loaded. {GetTime()}", "", 1000, MessageBoxIcon.Information);
								m_Object.LoadBones();
								AfterCopy();
							}
							else
								AutoClosingMessageBox.Show($"Failed to load bone data.{GetRetCode(code)}", "", GetErrorTime(), MessageBoxIcon.Error);
						}
					});
					SdkThread.Start();
					break;
				}
				else if (Path.GetExtension(fileList[i]) == ".skls" || Path.GetExtension(fileList[i]) == ".skl")
				{
					if (m_Object.bones.Count <= 0) return;
					SklsToLoad.Add(fileList[i]);
				}
			}

			if (SklsToLoad.Count > 0)
            {
				if (m_Object.bones.Count <= 0) return;
				SdkThread = new Thread(() => {
					int code = StartEditor(true, EditorMode.LoadMotions, m_Object.TEMP_FILE_NAME);
					if (!EditorKilled[0])
					{
						if (code == 0)
						{
							AutoClosingMessageBox.Show($"Motions successfully loaded. {GetTime()}", "", 1000, MessageBoxIcon.Information);
							DeletesklsToolStripMenuItem.Enabled = true;
							SaveSklsToolStripMenuItem.Enabled = true;
							sklToolStripMenuItem.Enabled = true;
							oMFToolStripMenuItem.Enabled = true;
						}
						else
							AutoClosingMessageBox.Show($"Can't load motions.{GetRetCode(code)}", "", GetErrorTime(), MessageBoxIcon.Error);

						m_Object.LoadMotions();
						AfterLoadMotions();
					}
				});
				SdkThread.Start();
			}
		}

		private void DragEnterCallback(object sender, DragEventArgs e)
		{
			if (e.Data.GetDataPresent(DataFormats.FileDrop)) e.Effect = DragDropEffects.Copy;
		}

		private void saveToolStripMenuItem1_Click(object sender, EventArgs e)
		{
			if (!CheckThread()) return;

			SdkThread = new Thread(() => { FastSaveObject(m_Object.FILE_NAME); });
			SdkThread.Start();
		}

		private void ProgressiveMeshes_CheckedChanged(object sender, EventArgs e)
		{
			CheckBox chbx = sender as CheckBox;

			if (chbx.Checked)
				StripifyMeshes.Checked = false;
		}

		private void StripifyMeshes_CheckedChanged(object sender, EventArgs e)
		{
			CheckBox chbx = sender as CheckBox;

			if (chbx.Checked)
				ProgressiveMeshes.Checked = false;
		}

		private void exitToolStripMenuItem_Click(object sender, EventArgs e)
		{
			if (MessageBox.Show("Are you sure you want to exit?", "Object Editor", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
				Close();
		}

		private void RichTextBoxImgDefender(object sender, KeyEventArgs e)
		{
			RichTextBox TextBox = sender as RichTextBox;
			if (e.Control && e.KeyCode == Keys.V)
			{
				if (Clipboard.ContainsText())
					TextBox.Paste(DataFormats.GetFormat(DataFormats.Text));
				e.Handled = true;
			}
		}

		private void MotionRefsTextChanged(object sender, EventArgs e)
		{
			motionRefsToolStripMenuItem1.Enabled = IsTextCorrect((sender as RichTextBox).Text) && MotionRefsBox.Enabled;
		}

		private void UserDataTextChanged(object sender, EventArgs e)
		{
			userDataToolStripMenuItem1.Enabled = IsTextCorrect((sender as RichTextBox).Text) && UserDataTextBox.Enabled;
		}

		private void PageResize(object sender, EventArgs e)
		{
			switch (TabControl.Controls[TabControl.SelectedIndex].Name)
			{
				case "BonesPage":
					BoneSize = this.Size;
					break;
				default:
					CurrentSize = this.Size;
					break;
			}
		}

		private void settingsToolStripMenuItem_Click(object sender, EventArgs e)
		{
			string old_game_mtl = "";
			pSettings.Load("GameMtlPath", ref old_game_mtl);

			Settings ProgramSettings = new Settings(this.pSettings, this, this);
			ProgramSettings.ShowDialog();

			string game_mtl = "";
			pSettings.Load("GameMtlPath", ref game_mtl);
			pSettings.LoadState("SplitNormalsChbx", ref NORMALS_DEFAULT, true);

			if (old_game_mtl != game_mtl)
				ReloadGameMtl(game_mtl);

			bool Debug = false;
			pSettings.LoadState("Debug", ref Debug);
			debugToolStripMenuItem.Visible = Debug;
		}

		public void SyncCompressUI(bool visible)
		{
			if (AnimsNoCompress.Visible && !visible) // disable
			{
				AnimsNoCompress.Visible = false;
				BuildInMotionsExport.Location = AnimsNoCompress.Location;
				MotionFlagsGroupBox.Size = new Size(MotionFlagsGroupBox.Size.Width, MotionFlagsGroupBox.Size.Height - 22);
				ScaleGroupBox.Location = new Point(ScaleGroupBox.Location.X, ScaleGroupBox.Location.Y - 22);
				ScaleGroupBox.Size = new Size(ScaleGroupBox.Size.Width, ScaleGroupBox.Size.Height + 22);
			}
			else if (!AnimsNoCompress.Visible && visible) // enable
			{
				AnimsNoCompress.Visible = true;
				BuildInMotionsExport.Location = new Point(6, 88);
				MotionFlagsGroupBox.Size = new Size(MotionFlagsGroupBox.Size.Width, MotionFlagsGroupBox.Size.Height + 22);
				ScaleGroupBox.Location = new Point(ScaleGroupBox.Location.X, ScaleGroupBox.Location.Y + 22);
				ScaleGroupBox.Size = new Size(ScaleGroupBox.Size.Width, ScaleGroupBox.Size.Height - 22);
			}
		}

		private void openImageFolderToolStripMenuItem_Click(object sender, EventArgs e)
		{
			string image_path = "";
			pSettings.Load("ImagePath", ref image_path);

			if (image_path != "" && Directory.Exists(image_path))
			{
				Process PrFolder = new Process();
				ProcessStartInfo psi = new ProcessStartInfo();
				psi.CreateNoWindow = true;
				psi.WindowStyle = ProcessWindowStyle.Normal;
				psi.FileName = "explorer";
				psi.Arguments = image_path;
				PrFolder.StartInfo = psi;
				PrFolder.Start();
			}
		}

		private void reloadToolStripMenuItem_Click(object sender, EventArgs e)
		{
			ViewtoolStripMenuItem_Click(null, null);
		}

		private void refreshTexturesToolStripMenuItem_Click(object sender, EventArgs e)
		{
			string Textures = "";
			pSettings.LoadText("TexturesPath", ref Textures);

			List<string> pTextures = new List<string>();

			if (m_Object.surfaces.Count > 0 && Textures != "")
			{
				for (int i = 0; i < m_Object.surfaces.Count; i++)
				{
					string texture_main = Textures + "\\" + m_Object.surfaces[i].texture + ".dds";
					string texture_temp = TempFolder() + "\\" + Path.GetFileName(m_Object.surfaces[i].texture + ".png");

					if (File.Exists(texture_main)) // Create png
					{
						pTextures.Add(texture_main);
						pTextures.Add(texture_temp);
					}
				}
			}

			StartEditor(false, EditorMode.CreateMTL, m_Object.TEMP_FILE_NAME, m_Object.TEMP_FILE_NAME, -1, 1.0f, pTextures.ToArray());
			ViewtoolStripMenuItem_Click(null, null); // reset viewer
		}

		private void ResizeEmbeddedApp(object sender, EventArgs e)
		{
			if (ViewerProcess == null || !ViewerWorking)
				return;
			int width = ViewPortPanel.Width;
			int height = ViewPortPanel.Height;
			SetWindowPos(ViewerProcess.MainWindowHandle, IntPtr.Zero, 0, 0, width, height, SWP_NOACTIVATE | SWP_NOZORDER);
		}

		private void ViewtoolStripMenuItem_Click(object sender, EventArgs e)
		{
			if (ViewerThread != null && ViewerThread.ThreadState != System.Threading.ThreadState.Stopped)
				ViewerThread.Abort();

			if (EditorWorking[1])
			{
				EditorKilled[1] = true;
				EditorProcess[1].Kill();
				EditorProcess[1].Close();
				EditorWorking[1] = false;
			}

			if (ViewerWorking)
			{
				ViewerProcess.Kill();
				ViewerProcess.Close();
				ViewerWorking = false;
			}

			ViewerThread = new Thread(() => {
                this.Invoke((MethodInvoker)delegate ()
                {
                    CreateViewPort.Enabled = false;
                });
                string ObjName = Path.ChangeExtension(m_Object.TEMP_FILE_NAME, ".obj");
				string exe_path = AppPath() + "\\f3d.exe";

				if (!File.Exists(exe_path))
				{
					MessageBox.Show("Can't find Viewport module.\nPlease, reinstall the app.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
					return;
				}

				string Textures = "";
				pSettings.LoadText("TexturesPath", ref Textures);

				List<string> pTextures = new List<string>();

				if (m_Object.surfaces.Count > 0 && Textures != "")
				{
					for (int i = 0; i < m_Object.surfaces.Count; i++)
					{
						string texture_main = Textures + "\\" + m_Object.surfaces[i].texture + ".dds";
						string texture_temp = TempFolder() + "\\" + Path.GetFileName(m_Object.surfaces[i].texture + ".png");

						if (File.Exists(texture_main)) // Create png
						{
							pTextures.Add(texture_main);
							pTextures.Add(texture_temp);
						}
					}
				}

				int code = StartEditor(false, EditorMode.ExportOBJOptimized, m_Object.TEMP_FILE_NAME, ObjName, -1, 1.0f, pTextures.ToArray(), 1);
				pTextures.Clear();

				if (code > 100 || code < -100) // Error, maybe textures...
					StartEditor(false, EditorMode.ExportOBJOptimized, m_Object.TEMP_FILE_NAME, ObjName, -1, 1.0f, pTextures.ToArray(), 1);

				if (ViewerWorking)
				{
					ViewerProcess.Kill();
					ViewerProcess.Close();
				}

				string image_path = "";
				pSettings.Load("ImagePath", ref image_path);

				bool first_load = true;
				pSettings.Load("FirstLoad", ref first_load, true);

				ViewerProcess.StartInfo.FileName = exe_path;
				ViewerProcess.StartInfo.Arguments = $"--input=\"{ObjName}\" --output=\"{image_path}\"" + (first_load ? " --filename" : "");
				ViewerProcess.StartInfo.UseShellExecute = false;
				ViewerProcess.StartInfo.WindowStyle = ProcessWindowStyle.Minimized;

				ViewerProcess.Start();
				ViewerWorking = true;
                this.Invoke((MethodInvoker)delegate ()
                {
                    CreateViewPort.Visible = false;
                });
                ViewerProcess.WaitForInputIdle();
				this.Invoke((MethodInvoker)delegate ()
				{
					SetParent(ViewerProcess.MainWindowHandle, ViewPortPanel.Handle);
					int style = GetWindowLong(ViewerProcess.MainWindowHandle, GWL_STYLE);
					style = style & ~WS_CAPTION & ~WS_THICKFRAME;
					SetWindowLong(ViewerProcess.MainWindowHandle, GWL_STYLE, style);
					ResizeEmbeddedApp(null, null);
				});
			});
			ViewerThread.Start();
		}

		private void BoneListIndexChanged(object sender, EventArgs e)
		{
			if (BonesList.SelectedIndex == -1) return;

			Object.Bone cur_bone = m_Object.bones[BonesList.SelectedIndex];

			NoPickable.Checked = (cur_bone.shape_flags & (1 << 0)) == (1 << 0);
			NoPhysics.Checked = (cur_bone.shape_flags & (1 << 1)) == (1 << 1);
			RemoveAfterBreak.Checked = (cur_bone.shape_flags & (1 << 2)) == (1 << 2);
			NoFogCollider.Checked = (cur_bone.shape_flags & (1 << 3)) == (1 << 3);
			ShapeType.SelectedIndex = cur_bone.shape_type;
			BoneName.Text = cur_bone.bone_name;
			BoneMaterial.Text = cur_bone.material;
			BoneMass.Text = ((decimal)cur_bone.mass).ToString();
			Breakable.Checked = cur_bone.breakable;
			JointType.SelectedIndex = cur_bone.joint_type;
			JointFriction.Text = (cur_bone.friction).ToString();
			JointSpring.Text = ((decimal)cur_bone.spring).ToString();
			JointDamping.Text = ((decimal)cur_bone.damping).ToString();
			PosX.Text = ((decimal)cur_bone.position.x).ToString();
			PosY.Text = ((decimal)cur_bone.position.y).ToString();
			PosZ.Text = ((decimal)cur_bone.position.z).ToString();
			RotX.Text = ((decimal)cur_bone.rotation.x).ToString();
			RotY.Text = ((decimal)cur_bone.rotation.y).ToString();
			RotZ.Text = ((decimal)cur_bone.rotation.z).ToString();
			MassX.Text = ((decimal)cur_bone.center_of_mass.x).ToString();
			MassY.Text = ((decimal)cur_bone.center_of_mass.y).ToString();
			MassZ.Text = ((decimal)cur_bone.center_of_mass.z).ToString();
		}

		private void JointType_SelectedIndexChanged(object sender, EventArgs e)
		{
			if (JointType.SelectedIndex == -1) return;

			switch (JointType.SelectedIndex)
			{
				case 0: // Rigid
					LimitsBox.Enabled = false;
					AxisX.Enabled = true;
					AxisX.Text = "Axis X";
					AxisY.Enabled = true;
					AxisY.Text = "Axis Y";
					AxisZ.Enabled = true;
					Steer.Enabled = true;
					JointFriction.Enabled = false;
					JointFrictionLabel.Enabled = false;
					JointSpring.Enabled = false;
					JointSpringLabel.Enabled = false;
					JointDamping.Enabled = false;
					JointDampingLabel.Enabled = false;
					break;
				case 1: // Cloth
					LimitsBox.Enabled = false;
					AxisX.Enabled = true;
					AxisX.Text = "Axis X";
					AxisY.Enabled = true;
					AxisY.Text = "Axis Y";
					AxisZ.Enabled = true;
					Steer.Enabled = true;
					JointFriction.Enabled = true;
					JointFrictionLabel.Enabled = true;
					JointSpring.Enabled = true;
					JointSpringLabel.Enabled = true;
					JointDamping.Enabled = true;
					JointDampingLabel.Enabled = true;
					break;
				case 2: // Joint
					LimitsBox.Enabled = true;
					AxisX.Enabled = true;
					AxisX.Text = "Axis X";
					AxisY.Enabled = true;
					AxisY.Text = "Axis Y";
					AxisZ.Enabled = true;
					Steer.Enabled = false;
					JointFriction.Enabled = true;
					JointFrictionLabel.Enabled = true;
					JointSpring.Enabled = true;
					JointSpringLabel.Enabled = true;
					JointDamping.Enabled = true;
					JointDampingLabel.Enabled = true;
					break;
				case 3: // Wheel
					LimitsBox.Enabled = true;
					AxisX.Enabled = true;
					AxisX.Text = "Axis X";
					AxisY.Enabled = false;
					AxisY.Text = "Axis Y";
					AxisZ.Enabled = false;
					Steer.Enabled = false;
					JointFriction.Enabled = true;
					JointFrictionLabel.Enabled = true;
					JointSpring.Enabled = true;
					JointSpringLabel.Enabled = true;
					JointDamping.Enabled = true;
					JointDampingLabel.Enabled = true;
					break;
				case 4: // Slider
					LimitsBox.Enabled = true;
					AxisX.Enabled = true;
					AxisX.Text = "Slide Z";
					AxisY.Enabled = true;
					AxisY.Text = "Rotate Z";
					AxisZ.Enabled = false;
					Steer.Enabled = false;
					JointFriction.Enabled = true;
					JointFrictionLabel.Enabled = true;
					JointSpring.Enabled = true;
					JointSpringLabel.Enabled = true;
					JointDamping.Enabled = true;
					JointDampingLabel.Enabled = true;
					break;
			}
		}





		// Interface
		private void CreateBoneGroupBox(int idx, Object.Bone bone)
		{
			var GroupBox = new GroupBox();
			GroupBox.Location = new System.Drawing.Point(3, 3 + 106 * idx);
			GroupBox.Size = new System.Drawing.Size(362, 100);
			GroupBox.Text = "Bone name: " + bone.bone_name;
			GroupBox.Name = "ShapeGrpBox_" + idx;
			GroupBox.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top;
			GroupBox.Dock = DockStyle.Top;
			CreateBoneControl(idx, GroupBox, bone);
			BonesPage.Controls.Add(GroupBox);
		}

		private void CreateBoneControl(int idx, GroupBox box, Object.Bone bone)
		{
			var NoPickableCheckBoxBox = new CheckBox();
			NoPickableCheckBoxBox.Name = "chbxNoPickable_" + idx;
			NoPickableCheckBoxBox.Text = "No Pickable";
			NoPickableCheckBoxBox.Size = new System.Drawing.Size(130, 23);
			NoPickableCheckBoxBox.Location = new System.Drawing.Point(6, 15);
			NoPickableCheckBoxBox.Anchor = AnchorStyles.Left;
			NoPickableCheckBoxBox.Checked = (bone.shape_flags & (1 << 0)) == (1 << 0);
			NoPickableCheckBoxBox.CheckedChanged += new System.EventHandler(this.CheckBoxCheckedChanged);

			var NoPhysicsCheckBoxBox = new CheckBox();
			NoPhysicsCheckBoxBox.Name = "chbxNoPhysics_" + idx;
			NoPhysicsCheckBoxBox.Text = "No Physics";
			NoPhysicsCheckBoxBox.Size = new System.Drawing.Size(100, 23);
			NoPhysicsCheckBoxBox.Location = new System.Drawing.Point(6, 35);
			NoPhysicsCheckBoxBox.Anchor = AnchorStyles.Left;
			NoPhysicsCheckBoxBox.Checked = (bone.shape_flags & (1 << 1)) == (1 << 1);
			NoPhysicsCheckBoxBox.CheckedChanged += new System.EventHandler(this.CheckBoxCheckedChanged);

			var RemoveAfterBreakCheckBoxBox = new CheckBox();
			RemoveAfterBreakCheckBoxBox.Name = "chbxRemoveAfterBreakCheckBoxBox_" + idx;
			RemoveAfterBreakCheckBoxBox.Text = "Remove After Break";
			RemoveAfterBreakCheckBoxBox.Size = new System.Drawing.Size(132, 23);
			RemoveAfterBreakCheckBoxBox.Location = new System.Drawing.Point(6, 55);
			RemoveAfterBreakCheckBoxBox.Anchor = AnchorStyles.Left;
			RemoveAfterBreakCheckBoxBox.Checked = (bone.shape_flags & (1 << 2)) == (1 << 2);
			RemoveAfterBreakCheckBoxBox.CheckedChanged += new System.EventHandler(this.CheckBoxCheckedChanged);

			var NoFogColliderCheckBoxBox = new CheckBox();
			NoFogColliderCheckBoxBox.Name = "chbxNoFogColliderCheckBox_" + idx;
			NoFogColliderCheckBoxBox.Text = "No Fog Collider";
			NoFogColliderCheckBoxBox.Size = new System.Drawing.Size(120, 23);
			NoFogColliderCheckBoxBox.Location = new System.Drawing.Point(6, 75);
			NoFogColliderCheckBoxBox.Anchor = AnchorStyles.Left;
			NoFogColliderCheckBoxBox.Checked = (bone.shape_flags & (1 << 3)) == (1 << 3);
			NoFogColliderCheckBoxBox.CheckedChanged += new System.EventHandler(this.CheckBoxCheckedChanged);

			var TypeLabel = new Label();
			TypeLabel.Name = "TypeLbl_" + idx;
			TypeLabel.Text = "Shape type:";
			TypeLabel.Size = new System.Drawing.Size(70, 17);
			TypeLabel.Location = new System.Drawing.Point(135, 18);
			TypeLabel.Anchor = AnchorStyles.Left;

			var TypeComboBox = new ComboBox();
			TypeComboBox.Name = "cbxType_" + idx;
			TypeComboBox.Size = new System.Drawing.Size(155, 23);
			TypeComboBox.Location = new System.Drawing.Point(200, 15);
			TypeComboBox.Anchor = AnchorStyles.Left | AnchorStyles.Right;
			TypeComboBox.Items.Add("None");
			TypeComboBox.Items.Add("Box");
			TypeComboBox.Items.Add("Sphere");
			TypeComboBox.Items.Add("Cylinder");
			TypeComboBox.SelectedIndex = bone.shape_type;
			TypeComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
			TypeComboBox.SelectedIndexChanged += new System.EventHandler(this.ComboBoxIndexChanged);

			var MaterialLabel = new Label();
			MaterialLabel.Name = "MaterialLbl_" + idx;
			MaterialLabel.Text = "Material:";
			MaterialLabel.Size = new System.Drawing.Size(50, 17);
			MaterialLabel.Location = new System.Drawing.Point(135, 45);
			MaterialLabel.Anchor = AnchorStyles.Left;

			var MateriaBox = new Control();
			if (game_materials.Count() == 0)
			{
				var MateriaTextBox = new TextBox();
				MateriaTextBox.Name = "MaterialTextBox_" + idx;
				MateriaTextBox.Text = bone.material;
				MateriaTextBox.Size = new System.Drawing.Size(155, 17);
				MateriaTextBox.Location = new System.Drawing.Point(200, 43);
				MateriaTextBox.Anchor = AnchorStyles.Left | AnchorStyles.Right;
				MateriaTextBox.TextChanged += new EventHandler(this.MaterialTextChanged);

				MateriaBox = MateriaTextBox;
			}
			else
			{
				var MateriaTextBox = new ComboBox();
				MateriaTextBox.Name = "MaterialTextBox_" + idx;
				MateriaTextBox.Text = bone.material;
				MateriaTextBox.Size = new System.Drawing.Size(155, 17);
				MateriaTextBox.Location = new System.Drawing.Point(200, 43);
				MateriaTextBox.Anchor = AnchorStyles.Left | AnchorStyles.Right;
				MateriaTextBox.Items.AddRange(game_materials);
				MateriaTextBox.DropDownStyle = ComboBoxStyle.DropDownList;
				MateriaTextBox.SelectedIndexChanged += new System.EventHandler(this.MaterialTextChanged);
				if (MateriaTextBox.Items.Contains(bone.material))
					MateriaTextBox.SelectedIndex = MateriaTextBox.Items.IndexOf(bone.material);
				else
					MateriaTextBox.Text = bone.material;

				MateriaBox = MateriaTextBox;
			}

			var MassLabel = new Label();
			MassLabel.Name = "MassLbl_" + idx;
			MassLabel.Text = "Mass:";
			MassLabel.Size = new System.Drawing.Size(50, 17);
			MassLabel.Location = new System.Drawing.Point(135, 72);
			MassLabel.Anchor = AnchorStyles.Left;

			var MassTextBox = new TextBox();
			MassTextBox.Name = "MassTextBox_" + idx;
			MassTextBox.Text = bone.mass.ToString();
			MassTextBox.Size = new System.Drawing.Size(155, 17);
			MassTextBox.Location = new System.Drawing.Point(200, 70);
			MassTextBox.Anchor = AnchorStyles.Left | AnchorStyles.Right;
			MassTextBox.KeyDown += new KeyEventHandler(this.TextBoxKeyDown);
			MassTextBox.KeyPress += new KeyPressEventHandler(this.TextBoxKeyPress);
			MassTextBox.TextChanged += new EventHandler(this.MassTextChanged);

			box.Controls.Add(NoPickableCheckBoxBox);
			box.Controls.Add(NoPhysicsCheckBoxBox);
			box.Controls.Add(RemoveAfterBreakCheckBoxBox);
			box.Controls.Add(NoFogColliderCheckBoxBox);
			box.Controls.Add(TypeComboBox);
			box.Controls.Add(TypeLabel);
			box.Controls.Add(MaterialLabel);
			box.Controls.Add(MateriaBox);
			box.Controls.Add(MassLabel);
			box.Controls.Add(MassTextBox);
		}

		private void TextureDragEnterEvent(object sender, DragEventArgs e)
		{
			if(e.Data.GetDataPresent(DataFormats.FileDrop))
				e.Effect = DragDropEffects.Copy;
			else
				e.Effect = DragDropEffects.None;
		}

		private void TexureDragDropEvent(object sender, DragEventArgs e)
        {
			string[] fileList = (string[])e.Data.GetData(DataFormats.FileDrop, false);
			TextBox box = sender as TextBox;
			box.Text = ParseXrayPath(fileList[0]);
		}

		private string ParseXrayPath(string filepath)
        {
			string res = filepath.Replace(".dds","");

			res = res.Substring(res.IndexOf("textures") + 9);

			return res;
        }

		private void CreateSurfaceGroupBox(int idx, Object.Surface surface)
		{
			var GroupBox = new GroupBox();
			GroupBox.Location = new System.Drawing.Point(3, 3 + 114 * idx);
			GroupBox.Size = new System.Drawing.Size(362, 108);
			GroupBox.Text = surface.name;
			GroupBox.Name = "MaterialGrpBox_" + idx;
			GroupBox.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top;
			GroupBox.Dock = DockStyle.Top;
			CreateSurfaceControl(idx, GroupBox, surface);
			SurfacesPage.Controls.Add(GroupBox);
		}

		private void CreateSurfaceControl(int idx, GroupBox box, Object.Surface surface)
		{
			var TwoSidedCheckBoxBox = new CheckBox();
			TwoSidedCheckBoxBox.Name = "chbx2sided_" + idx;
			TwoSidedCheckBoxBox.Text = "2 Sided";
			TwoSidedCheckBoxBox.Size = new System.Drawing.Size(130, 23);
			TwoSidedCheckBoxBox.Location = new System.Drawing.Point(8, 82);
			TwoSidedCheckBoxBox.Anchor = AnchorStyles.Left | AnchorStyles.Right;
			TwoSidedCheckBoxBox.Checked = (surface.flags == 1);
			TwoSidedCheckBoxBox.CheckedChanged += new System.EventHandler(this.CheckBoxCheckedChanged);

			var TextureLabel = new Label();
			TextureLabel.Text = "Texture:";
			TextureLabel.Name = "TextureLabel_" + idx;
			TextureLabel.Size = new System.Drawing.Size(46, 23);
			TextureLabel.Location = new System.Drawing.Point(5, 32);
			TextureLabel.Anchor = AnchorStyles.Left;

			var TextureTextBox = new TextBox();
			TextureTextBox.Name = "TextureTextBox_" + idx;
			TextureTextBox.Size = new System.Drawing.Size(299, 23);
			TextureTextBox.Location = new System.Drawing.Point(55, 30);
			TextureTextBox.TextChanged += new System.EventHandler(this.TextBoxFilter);
			TextureTextBox.Anchor = AnchorStyles.Left | AnchorStyles.Right;
			TextureTextBox.DragEnter += new DragEventHandler(TextureDragEnterEvent);
			TextureTextBox.DragDrop += new DragEventHandler(TexureDragDropEvent);
			TextureTextBox.AllowDrop = true;

			string texture_path = surface.texture;
			if (surface.texture.LastIndexOf('.') != -1)
			{
				string format = surface.texture.Substring(surface.texture.LastIndexOf('.') + 1, surface.texture.Length - surface.texture.LastIndexOf('.') - 1);
				if (format.Length == 3) // Хз нужно ли или нет, но у основных форматов длинна названия в 3 символа, пусть будет проверкой
					texture_path = surface.texture.Substring(0, surface.texture.LastIndexOf('.'));
			}
			TextureTextBox.Text = texture_path;

			var ShaderLabel = new Label();
			ShaderLabel.Text = "Shader:";
			ShaderLabel.Name = "ShaderLabel_" + idx;
			ShaderLabel.Size = new System.Drawing.Size(46, 23);
			ShaderLabel.Location = new System.Drawing.Point(5, 60);
			ShaderLabel.Anchor = AnchorStyles.Left;

			var ShaderTextBox = new TextBox();
			ShaderTextBox.Name = "ShaderTextBox_" + idx;
			ShaderTextBox.Size = new System.Drawing.Size(299, 23);
			ShaderTextBox.Location = new System.Drawing.Point(55, 58);
			ShaderTextBox.Text = surface.shader;
			ShaderTextBox.Anchor = AnchorStyles.Left | AnchorStyles.Right;
			ShaderTextBox.TextChanged += new System.EventHandler(this.TextBoxFilter);

			Object.Mesh.SurfFace surfFace = m_Object.GetSurfFaceByName(surface.name);
			var FaceLabel = new Label();
			FaceLabel.Text = "Face count: " + (surfFace != null ? (surfFace.faces * ((surface.flags == 1) ? 2 : 1)).ToString() : "null");
			FaceLabel.Name = "FaceLabel_" + idx;
			FaceLabel.Size = new System.Drawing.Size(112, 20);
			FaceLabel.Location = new System.Drawing.Point(245, 83);
			FaceLabel.Anchor = AnchorStyles.Right;
			FaceLabel.ImageAlign = System.Drawing.ContentAlignment.MiddleRight;
			FaceLabel.TextAlign = System.Drawing.ContentAlignment.MiddleRight;

			box.Controls.Add(TwoSidedCheckBoxBox);
			box.Controls.Add(TextureLabel);
			box.Controls.Add(TextureTextBox);
			box.Controls.Add(ShaderLabel);
			box.Controls.Add(ShaderTextBox);
			box.Controls.Add(FaceLabel);
		}
    }
}
