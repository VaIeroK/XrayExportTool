//---------------------------------------------------------------------------
#pragma once

#include "../../../xrRender/Private/blenders\blender.h"
#include "../../../xrRender/Private/blenders\blender_clsid.h"
#include "../../../xrRender/Private/xrRender_console.h"
#include "../../../xrRender/Private/PSLibrary.h"
#include "../../../xrRender/Private/IRenderDetailModel.H"
#include "../../../xrRender/Private/DetailModel.H"
#include "../../../xrRender/Private/ModelPool.h"
#include "../../../xrRender/Private/SkeletonCustom.h"
#include "../../../xrAPI/xrAPI.h"
class ISpatial;

// definition (Renderer)
class CRenderTarget
{
public:
	CRenderTarget() {}
	virtual	void					set_blur(float	f) {}
	virtual	void					set_gray(float	f) {}
	virtual void					set_duality_h(float	f) {}
	virtual void					set_duality_v(float	f) {}
	virtual void					set_noise(float	f) {}
	virtual void					set_noise_scale(float	f) {}
	virtual void					set_noise_fps(float	f) {}
	virtual void					set_color_base(u32	f) {}
	virtual void					set_color_gray(u32	f) {}
	//virtual void					set_color_add		(u32	f)							= 0;
	virtual void					set_color_add(const Fvector& f) {}
	virtual void					set_cm_imfluence(float	f) {}
	virtual void					set_cm_interpolate(float	f) {}
	virtual void					set_cm_textures(const shared_str& tex0, const shared_str& tex1) {}
	virtual ~CRenderTarget() {};
	virtual u32			get_width			()				{ return 0;	}
	virtual u32			get_height			()				{ return 0;	}
};



class	ECORE_API CRender
{
	CRenderTarget* Target;
	Fmatrix					current_matrix;
	BOOL val_bInvisible;
public:
	// options


	CModelPool* Models;
public:
	// Occlusion culling
	virtual BOOL			occ_visible(Fbox& B);
	virtual BOOL			occ_visible(vis_data& P);

	// Constructor/destructor
	CRender();
	virtual 				~CRender();


	void 					Initialize();
	void 					ShutDown();

	void					OnDeviceCreate();
	void					OnDeviceDestroy();

	virtual	void					Calculate();
	virtual void					Render();

	virtual void					set_Transform(Fmatrix* M);
	virtual void					add_Visual(IRenderVisual* visual);

	virtual ref_shader		getShader(int id);
	virtual	CRenderTarget* getTarget() { return Target; }
	//.	virtual IRender_Target*	getTarget		(){return Target;}


	void					reset_begin();
	void					reset_end();
	virtual IRenderVisual* model_Create(LPCSTR name, IReader* data = 0);
	virtual IRenderVisual* model_CreateChild(LPCSTR name, IReader* data);
	virtual IRenderVisual* model_CreatePE(LPCSTR name);
	virtual IRenderVisual* model_CreateParticles(LPCSTR name);

	virtual IRender_DetailModel* model_CreateDM(IReader* R);
	virtual IRenderVisual* model_Duplicate(IRenderVisual* V);
	virtual void			model_Delete(IRenderVisual*& V, BOOL bDiscard = TRUE);
	virtual void			model_Delete(IRender_DetailModel*& F)
	{
		if (F)
		{
			CDetail* D = (CDetail*)F;
			D->Unload();
			xr_delete(D);
			F = NULL;
		}
	}
	void 					model_Render(IRenderVisual* m_pVisual, const Fmatrix& mTransform, int priority, bool strictB2F, float m_fLOD);
	void 					model_RenderSingle(IRenderVisual* m_pVisual, const Fmatrix& mTransform, float m_fLOD);
	virtual bool			is_sun_static() { return true; };

	virtual void			add_SkeletonWallmark(intrusive_ptr<CSkeletonWallmark> wm) {};
	virtual void			add_SkeletonWallmark(const Fmatrix* xf, CKinematics* obj, ref_shader& sh, const Fvector& start, const Fvector& dir, float size) {};

	virtual void			add_SkeletonWallmark(const Fmatrix* xf, IKinematics* obj, IWallMarkArray* pArray, const Fvector& start, const Fvector& dir, float size) {}
	// Render mode
	virtual void			rmNear();
	virtual void			rmFar();
	virtual void			rmNormal();

	void 					apply_lmaterial() {}

	virtual LPCSTR			getShaderPath()
	{
		return "editor\\";
	}


	virtual HRESULT			CompileShader(
		LPCSTR          	pSrcData,
		UINT                SrcDataLen,
		void* pDefines,
		void* pInclude,
		LPCSTR              pFunctionName,
		LPCSTR              pTarget,
		DWORD               Flags,
		void* ppShader,
		void* ppErrorMsgs,
		void* ppConstantTable);

	virtual IDirect3DBaseTexture9* texture_load(LPCSTR	fname, u32& mem_size);
	virtual HRESULT					shader_compile(
		LPCSTR							name,
		LPCSTR                          pSrcData,
		UINT                            SrcDataLen,
		void* pDefines,
		void* pInclude,
		LPCSTR                          pFunctionName,
		LPCSTR                          pTarget,
		DWORD                           Flags,
		void* ppShader,
		void* ppErrorMsgs,
		void* ppConstantTable);

	virtual DWORD					get_dx_level();

	// Loading / Unloading
	virtual	void					create();
	virtual	void					destroy();

		virtual	void					level_Load(IReader*);
	virtual void					level_Unload();

	//virtual IDirect3DBaseTexture9*	texture_load			(LPCSTR	fname, u32& msize)					= 0;

	// Information
	virtual	void					Statistics(CGameFont* F) {};

	//	virtual ref_shader				getShader				(int id)									= 0;

	// Main 
	virtual void					set_HUD(BOOL 		V);
	virtual BOOL					get_HUD();
	virtual void					set_Invisible(BOOL 		V);
	virtual void					flush();
	virtual	void					add_Occluder(Fbox2& bb_screenspace);	// mask screen region as oclluded (-1..1, -1..1)
	virtual void					add_Geometry(IRenderVisual* V);	// add visual(s)	(all culling performed)
//	virtual void					add_StaticWallmark		(ref_shader& S, const Fvector& P, float s, CDB::TRI* T, Fvector* V)=0;
	virtual void					clear_static_wallmarks() {}
	//virtual void					add_SkeletonWallmark	(intrusive_ptr<CSkeletonWallmark> wm)						= 0;
	//virtual void					add_SkeletonWallmark	(const Fmatrix* xf, CKinematics* obj, ref_shader& sh, const Fvector& start, const Fvector& dir, float size)=0;

	//virtual IBlender*				blender_create			(CLASS_ID cls)								= 0;
	//virtual void					blender_destroy			(IBlender* &)								= 0;

	// Models
	virtual void					model_Logging(BOOL bEnable);
	virtual void					models_Prefetch();
	virtual void					models_Clear(BOOL b_complete);

	// Main
	virtual void					ScreenshotAsyncBegin();
	virtual void					ScreenshotAsyncEnd(CMemoryWriter& memory_writer);

	// Render mode
	virtual u32						memory_usage();

protected:
	virtual HRESULT					shader_compile(
		LPCSTR							name,
		DWORD const* pSrcData,
		UINT                            SrcDataLen,
		LPCSTR                          pFunctionName,
		LPCSTR                          pTarget,
		DWORD                           Flags,
		void*& result
	) {
		return E_FAIL;
	}
	private:
		xr_vector<ISpatial*> lstRenderables;
};
#ifdef REDITOR
IC  float   CalcSSA(Fvector& C, float R)
{
    float distSQ  = EDevice->m_Camera.GetPosition().distance_to_sqr(C);
    return  R*R/distSQ;
}
#endif
extern ECORE_API CRender  	RImplementation;
//.extern ECORE_API CRender*	Render;

