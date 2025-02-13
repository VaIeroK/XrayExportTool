//----------------------------------------------------
// file: Library.cpp
//----------------------------------------------------

#include "stdafx.h"
#pragma hdrstop

#include "Library.h"
#include "EditObject.h"
#include "ui_main.h"

//----------------------------------------------------
ELibrary Lib;
//----------------------------------------------------
ELibrary::ELibrary()
{
    m_bReady  = false;
}
//----------------------------------------------------

ELibrary::~ELibrary()
{
}
//----------------------------------------------------

void ELibrary::OnCreate()
{
	//EDevice->seqDevCreate.Add	(this,REG_PRIORITY_NORMAL);
	//EDevice->seqDevDestroy.Add(this,REG_PRIORITY_NORMAL);
    m_bReady = true;
}
//----------------------------------------------------

void ELibrary::OnDestroy()
{
}
//----------------------------------------------------

void ELibrary::CleanLibrary()
{
}
//----------------------------------------------------
void ELibrary::ReloadObject(LPCSTR nm)
{
}
//---------------------------------------------------------------------------
void ELibrary::ReloadObjects(){
}
//----------------------------------------------------

void ELibrary::OnDeviceCreate(){
}
//---------------------------------------------------------------------------

void ELibrary::OnDeviceDestroy(){
}
//---------------------------------------------------------------------------

void ELibrary::EvictObjects()
{
}
//----------------------------------------------------


//---------------------------------------------------------------------------

void ELibrary::Save(FS_FileSet* modif_map)
{
}
//---------------------------------------------------------------------------

int ELibrary::GetObjects(FS_FileSet& files)
{
    return FS.file_list(files,_objects_,FS_ListFiles|FS_ClampExt,"*.object");
}
//---------------------------------------------------------------------------
//---------------------------------------------------------------------------
//---------------------------------------------------------------------------

void ELibrary::UnloadEditObject(LPCSTR full_name)
{
}
//---------------------------------------------------------------------------

