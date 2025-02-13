//----------------------------------------------------
// file: EditObject.cpp
//----------------------------------------------------

#include "stdafx.h"
#pragma hdrstop

#include "..\..\xrEngine\fmesh.h"

#include "EditObject.h"
#include "EditMesh.h"

#if 1
	#include "..\xrEngine\motion.h"
	#include "..\xrEngine\bone.h"
#endif
#include "..\XrECore\VisualLog.h"

// mimimal bounding box size
float g_MinBoxSize 	= 0.05f;

#if 1
void CSurface::CreateImageData()
{
}
void CSurface::RemoveImageData()
{
}
#endif

CEditableObject::CEditableObject(LPCSTR name):
    m_object_xform(0)
{
    m_FaceCount = -1;
    m_VertexCount = -1;
	m_LibName		= name;

	m_objectFlags.zero	();
    m_ObjectVersion	= 0;

#if 1
    vs_SkeletonGeom	= 0;
#endif
	m_BBox.invalidate();

    m_LoadState.zero();

    m_ActiveSMotion = 0;

	t_vPosition.set	(0.f,0.f,0.f);
    t_vScale.set   	(1.f,1.f,1.f);
    t_vRotate.set  	(0.f,0.f,0.f);

	a_vPosition.set	(0.f,0.f,0.f);
    a_vRotate.set  	(0.f,0.f,0.f);
    a_vScale         = 1.f;
    a_vAdjustMass    = TRUE;

    bOnModified		= false;

    m_RefCount		= 0;

    m_LODShader		= 0;
    
    m_CreateName	= "unknown";
    m_CreateTime	= 0;
	m_ModifName		= "unknown";
    m_ModifTime		= 0;
    m_EditorScript = "";
    m_SmoothMsgSended = false;
    m_SmoothExportMsgSended = false;
}

CEditableObject::~CEditableObject()
{
    ClearGeometry();
}
//----------------------------------------------------

void CEditableObject::VerifyMeshNames()
{
	int idx=0;
	string1024 	nm,pref; 
    for(EditMeshIt m_def=m_Meshes.begin();m_def!=m_Meshes.end();m_def++){
		strcpy	(pref,(*m_def)->m_Name.size()?(*m_def)->m_Name.c_str():"mesh");
        _Trim	(pref);
		strcpy	(nm,pref);
		while (FindMeshByName(nm,*m_def))
			sprintf(nm,"%s%2d",pref,idx++);
        (*m_def)->SetName(nm);
    }
}

bool CEditableObject::ContainsMesh(const CEditableMesh* m)
{
    VERIFY(m);
    for(EditMeshIt m_def=m_Meshes.begin();m_def!=m_Meshes.end();m_def++)
        if (m==(*m_def)) return true;
    return false;
}

CEditableMesh* CEditableObject::FindMeshByName	(const char* name, CEditableMesh* Ignore)
{
    for(EditMeshIt m=m_Meshes.begin();m!=m_Meshes.end();m++)
        if ((Ignore!=(*m))&&(stricmp((*m)->Name().c_str(),name)==0)) return (*m);
    return 0;
}

void CEditableObject::ClearGeometry ()
{
#if 1
    OnDeviceDestroy();
#endif
    if (!m_Meshes.empty())
        for(EditMeshIt 	m=m_Meshes.begin(); m!=m_Meshes.end();m++)xr_delete(*m);
    if (!m_Surfaces.empty())
        for(SurfaceIt 	s_it=m_Surfaces.begin(); s_it!=m_Surfaces.end(); s_it++)
            xr_delete(*s_it);
    m_Meshes.clear();
    m_Surfaces.clear();
#if 1
    // bones
    for(BoneIt b_it=m_Bones.begin(); b_it!=m_Bones.end();b_it++)xr_delete(*b_it);
    m_Bones.clear();
    // skeletal motions
    for(SMotionIt s_it=m_SMotions.begin(); s_it!=m_SMotions.end();s_it++) xr_delete(*s_it);
    m_SMotions.clear();
#endif
    m_ActiveSMotion = 0;
}

int CEditableObject::GetFaceCount(bool bMatch2Sided, bool bIgnoreOCC)
{
    if (m_FaceCount!=-1)return m_FaceCount;
    m_FaceCount =0;
    for(EditMeshIt m = m_Meshes.begin();m!=m_Meshes.end();m++)
        m_FaceCount +=(*m)->GetFaceCount(bMatch2Sided, bIgnoreOCC);
	return m_FaceCount;
}

int CEditableObject::GetSurfFaceCount(const char* surf_name){
	int cnt=0;
    CSurface* surf = FindSurfaceByName(surf_name);
    for(EditMeshIt m = m_Meshes.begin();m!=m_Meshes.end();m++)
        cnt+=(*m)->GetSurfFaceCount(surf);
	return cnt;
}

int CEditableObject::GetVertexCount()
{
    if (m_VertexCount != -1)return m_VertexCount;
    m_VertexCount =0;
    for(EditMeshIt m = m_Meshes.begin();m!=m_Meshes.end();m++)
        m_VertexCount +=(*m)->GetVertexCount();
	return m_VertexCount;
}

void CEditableObject::UpdateBox()
{
	VERIFY			(!m_Meshes.empty());
    EditMeshIt m	= m_Meshes.begin();
    m_BBox.invalidate();

    for(; m!=m_Meshes.end(); ++m)
	{
        Fbox meshbox;
        (*m)->GetBox(meshbox);
        for(int i=0; i<8; ++i)
		{
            Fvector pt;
            meshbox.getpoint(i, pt);
            m_BBox.modify(pt);
        }
    }
}
//----------------------------------------------------
void CEditableObject::RemoveMesh(CEditableMesh* mesh){
	EditMeshIt m_it = std::find(m_Meshes.begin(),m_Meshes.end(),mesh);
    VERIFY(m_it!=m_Meshes.end());
	m_Meshes.erase(m_it);
    xr_delete(mesh);
}

void CEditableObject::TranslateToWorld(const Fmatrix& parent)
{
	EditMeshIt m = m_Meshes.begin();
	for(;m!=m_Meshes.end();m++) (*m)->Transform( parent );
#if 1
	OnDeviceDestroy();
#endif
	UpdateBox();
}

CSurface*	CEditableObject::FindSurfaceByName(const char* surf_name, int* s_id){
	for(SurfaceIt s_it=m_Surfaces.begin(); s_it!=m_Surfaces.end(); s_it++)
    	if (stricmp((*s_it)->_Name(),surf_name)==0){ if (s_id) *s_id=s_it-m_Surfaces.begin(); return *s_it;}
    return 0;
}

LPCSTR CEditableObject::GenerateSurfaceName(const char* base_name)
{
	static string1024 nm;
	strcpy(nm, base_name);
	if (FindSurfaceByName(nm)){
		DWORD idx=0;
		do{
			sprintf(nm,"%s_%d",base_name,idx);
			idx++;
		}while(FindSurfaceByName(nm));
	}
	return nm;
}

bool CEditableObject::VerifyBoneParts()
{
	U8Vec b_use(BoneCount(),0);
    for (BPIt bp_it=m_BoneParts.begin(); bp_it!=m_BoneParts.end(); bp_it++)
        for (int i=0; i<int(bp_it->bones.size()); i++){
        	int idx = FindBoneByNameIdx(bp_it->bones[i].c_str());
            if (idx==-1){
            	bp_it->bones.erase(bp_it->bones.begin()+i);
            	i--;
            }else{
	        	b_use[idx]++;
            }
        }

    for (U8It u_it=b_use.begin(); u_it!=b_use.end(); u_it++)
    	if (*u_it!=1) return false;
    return true;
}

void CEditableObject::PrepareOGFDesc(ogf_desc& desc)
{
	string512			tmp;
	desc.source_file	= m_LibName.c_str();
    desc.create_name	= m_CreateName.c_str();
    desc.create_time	= m_CreateTime;
    desc.modif_name		= m_ModifName.c_str();
    desc.modif_time		= m_ModifTime;
    desc.build_name		= strconcat(sizeof(tmp),tmp,"\\\\",Core.CompName,"\\",Core.UserName);
    ctime(&desc.build_time);
}

void CEditableObject::SetVersionToCurrent(BOOL bCreate, BOOL bModif)
{
	string512			tmp;
	if (bCreate || m_CreateName == "" || m_CreateName == NULL || m_CreateName == "unknown"){
		m_CreateName	= strconcat(sizeof(tmp),tmp,"\\\\",Core.CompName,"\\",Core.UserName);
		m_CreateTime	= time(NULL);
	}
	if (bModif || m_ModifName == "" || m_ModifName == NULL || m_ModifName == "unknown") {
		m_ModifName		= strconcat(sizeof(tmp),tmp,"\\\\",Core.CompName,"\\",Core.UserName);
		m_ModifTime		= time(NULL);
	}
}

void CEditableObject::GetFaceWorld(const Fmatrix& parent, CEditableMesh* M, int idx, Fvector* verts)
{
	const Fvector* PT[3];
	M->GetFacePT(idx, PT);
	parent.transform_tiny(verts[0],*PT[0]);
    parent.transform_tiny(verts[1],*PT[1]);
	parent.transform_tiny(verts[2],*PT[2]);
}

void CEditableObject::Optimize()
{
    WriteLog("..Optimize Model");
    for(EditMeshIt m_def=m_Meshes.begin();m_def!=m_Meshes.end();m_def++){
        (*m_def)->OptimizeMesh    (false);
        (*m_def)->RebuildVMaps    ();
    }
}

bool CEditableObject::Validate()
{
	bool bRes = true;
    for(SurfaceIt 	s_it=m_Surfaces.begin(); s_it!=m_Surfaces.end(); s_it++)
        if (false==(*s_it)->Validate()){ 
        	Msg("!Invalid surface found: Object [%s], Surface [%s].",GetName(),(*s_it)->_Name());
        	bRes=false;
        }
    for(EditMeshIt m_def=m_Meshes.begin();m_def!=m_Meshes.end();m_def++)
        if (false==(*m_def)->Validate()){ 
        	Msg("!Invalid mesh found: Object [%s], Mesh [%s].",m_LibName.c_str(),(*m_def)->Name().c_str());
        	bRes=false;
        }
    return bRes;
}
//----------------------------------------------------------------------------
//#ifdef DEBUG

LPCSTR	CEditableObject::LL_BoneName_dbg(u16 ID)
{
	return 	GetBone( ID )->Name().c_str();
}

//#endif
CBoneInstance&	CEditableObject::LL_GetBoneInstance(u16 bone_id)
{
    return *GetBone(bone_id);
}
CBoneData&	CEditableObject::LL_GetData(u16 bone_id)
{
  VERIFY(false);
  static   CBoneData   dummy_bone_data(0);
  return dummy_bone_data;
}

Fmatrix&	CEditableObject::LL_GetTransform_R(u16 bone_id)
{
   //	VERIFY(false);
   // static Fmatrix dummy_matrix;
    return GetBone(bone_id)->_RenderTransform();
}
Fobb&	CEditableObject::LL_GetBox(u16 bone_id)
{
	VERIFY(false);
    static  Fobb  dummy_box;
    return dummy_box;
}

void CEditableObject::ChangeSurfaceFlags(xr_vector<SurfaceParams> params)
{
    for (int i = 0; i < params.size(); i++)
    {
        m_Surfaces[i]->m_Flags.assign(params[i].flags);
        m_Surfaces[i]->m_Texture = params[i].texture;
        m_Surfaces[i]->m_ShaderName = params[i].shader;
    }
}

void CEditableObject::InitScript()
{
    if (m_EditorScript != "")
    {
        CInifile* ini = CInifile::Create(m_EditorScript.c_str());
        if (ini->section_exist("create_bones"))
        {
            CInifile::Sect& sect = ini->r_section("create_bones");
            for (auto it = sect.Data.begin(); it != sect.Data.end(); it++)
            {
                shared_str bone = it->second.c_str();
                if (!bone) // ������ ����� ���� ������� ���� �����
                    m_Bones.clear();

                u16 parent_bone = BoneIDByName(it->second.c_str());
                CBone* parent = m_Bones.size() > 0 ? GetBone(parent_bone) : NULL;
                AddBone(parent, it->first);
                Msg("Script: Bone [%s] created", it->first.c_str());
            }
        }

        if (ini->section_exist("delete_bones"))
        {
            CInifile::Sect& sect = ini->r_section("delete_bones");
            for (auto it = sect.Data.begin(); it != sect.Data.end(); it++)
            {
                u16 parent_bone = BoneIDByName(it->first.c_str());
                CBone* bone_to_del = GetBone(parent_bone);
                DeleteBone(bone_to_del);
                Msg("Script: Bone [%s] deleted", it->first.c_str());

                shared_str main_bone = !it->second ? BoneNameByID(0) : it->second;
                for (EditMeshIt mesh_it = FirstMesh(); mesh_it != LastMesh(); mesh_it++)
                {
                    CEditableMesh* MESH = *mesh_it;
                    MESH->CheckWMaps(main_bone);
                }
            }
        }

        if (ini->section_exist("assign_model"))
        {
            shared_str bone = ini->r_string("assign_model", "assign_to");
            for (EditMeshIt mesh_it = FirstMesh(); mesh_it != LastMesh(); mesh_it++)
            {
                CEditableMesh* MESH = *mesh_it;
                MESH->AssignMesh(bone);
            }
        }
    }
}

bool CEditableObject::IsAnimated()	
{
    if (m_objectFlags.is(eoExpBuildinMots))
        return !!SMotionCount() || !!m_SMotionRefs.size();
    else
        return !!m_SMotionRefs.size();
}

bool CEditableObject::LoadBoneParts(LPCSTR full_name)
{
    PrepareBoneParts();

    if (FS.exist(full_name)) 
    {
        for (int k = 0; k < 4; k++) { m_List[k].clear(); m_Name[k][0] = 0; }
        CInifile ini(full_name, TRUE, TRUE, FALSE);
        string64		buff;
        for (int i = 0; i < 4; ++i)
        {
            sprintf(buff, "part_%d", i);
            sprintf(m_Name[i], "%s", ini.r_string(buff, "partition_name"));
            CInifile::Sect& S = ini.r_section(buff);
            CInifile::SectCIt it = S.Data.begin();
            CInifile::SectCIt e = S.Data.end();
            for (; it != e; ++it)
            {
                if (0 != stricmp(it->first.c_str(), "partition_name"))
                {
                    m_List[i].push_back(it->first);
                }
            }
        }
    }
    return UpdateBoneParts();
}

bool CEditableObject::SaveBoneParts(LPCSTR full_name)
{
    PrepareBoneParts();

    CInifile ini(full_name, FALSE, FALSE, TRUE);
    string64		buff;
    for (int i = 0; i < 4; ++i)
    {
        sprintf(buff, "part_%d", i);
        ini.w_string(buff, "partition_name", m_Name[i]);
        for (auto node : m_List[i])
        {
            ini.w_string(buff, node.name.c_str(), NULL);
        }
    }
    return true;
}

bool CEditableObject::ToDefaultBoneParts()
{
    PrepareBoneParts();

    for (int k = 0; k < 4; k++) { m_List[k].clear(); m_Name[k][0] = 0; }
    xr_strcpy(m_Name[0], "default");
    for (BoneIt it = FirstBone(); it != LastBone(); it++)
    {
        m_List[0].push_back((*it)->Name());
    }
    return UpdateBoneParts();
}

bool CEditableObject::UpdateBoneParts()
{
    for (int k = 0; k < 4; k++)
    {
        if (m_List[k].size()&&!xr_strlen(m_Name[k])) 
        {
            ELog.DlgMsg(mtError, "Verify parts name.");
            return false;
        }
        for (int i = 0; i < 4; i++)
        {
            if (i == k)continue;
            if (!m_List[k].size()) continue;
            string_path Name[2];
            xr_strcpy(Name[0], m_Name[k]);
            xr_strcpy(Name[1], m_Name[i]);
            _strupr_s(Name[0]); _strupr_s(Name[1]);
            if (xr_strcmp(Name[0], Name[1])==0)
            {
                ELog.DlgMsg(mtError, "Unique name required.");
                return false;
            }
        }
    }

    // verify
    U8Vec b_use(BoneCount(), 0);
    for (int k = 0; k < 4; k++)
    {
        if (m_List[k].size())
        {
            for (auto node : m_List[k])
            {
                b_use[FindBoneByNameIdx(node.name.c_str())]++;
            }

        }

    }
    for (U8It u_it = b_use.begin(); u_it != b_use.end(); u_it++)
    {
        if (*u_it != 1)
        {
            ELog.DlgMsg(mtError, "Invalid bone part found (missing or duplicate bones).");
            return false;
        }
    }
    // save    
    m_BoneParts.clear();
    for (int k = 0; k < 4; k++) 
    {
        if (m_List[k].size())
        {
            m_BoneParts.push_back(SBonePart());
            SBonePart& BP = m_BoneParts.back();
            BP.alias = m_Name[k];
            for (auto node : m_List[k])
            {
                BP.bones.push_back(node.name);
            }

        }
    }

    return true;
}

bool CEditableObject::PrepareBoneParts()
{
    for (int k = 0; k < 4; k++) { m_List[k].clear(); m_Name[k][0] = 0; }
    for (BPIt it = m_BoneParts.begin(); it != m_BoneParts.end(); it++) 
    {
        xr_strcpy(m_Name[it - m_BoneParts.begin()], it->alias.c_str());
        for (RStringVecIt w_it = it->bones.begin(); w_it != it->bones.end(); w_it++)
            m_List[it - m_BoneParts.begin()].push_back(*w_it);
    }
    return true;
}

bool CEditableObject::BonePartsExist()
{
    return m_BoneParts.size() > 0;
}