﻿// ActorEditor.cpp : Определяет точку входа для приложения.
//
#include "stdafx.h"
#include <string>
#include <fstream>
#include <ctime>   
#include "../XrECore/VisualLog.h"
#include "..\BearBundle\BearCore\BearCore.hpp"
#include "..\BearBundle\BearGraphics\BearGraphics.hpp"

#if !defined(_DEBUG) && defined(_WIN64) 
#include "tbb/parallel_for.h" 
#include "tbb/blocked_range.h"
#endif

extern ECORE_API BOOL g_force16BitTransformQuant;
extern ECORE_API BOOL g_forceFloatTransformQuant;
extern ECORE_API float g_EpsSkelPositionDelta;

int iReaderPos = 0;

xr_vector<CEditableObject::ShapeEditType> LoadShapes(xr_vector<LPCSTR> args, int count)
{
    xr_vector<CEditableObject::ShapeEditType> vec;

    for (int i = 0; i < count; i++)
    {
        CEditableObject::ShapeEditType shape;
        std::string first = args[iReaderPos];
        first.erase(first.find_first_of("-"), first.size());
        shape.bone_id = atoi(first.c_str());

        std::string second = args[iReaderPos];
        second.erase(second.find_last_of("-"), second.size());
        second.erase(0, first.size() + 1);
        shape.bone_type = atoi(second.c_str());

        std::string third = args[iReaderPos];
        third.erase(0, third.find_last_of("-") + 1);
        shape.bone_flags.assign(atoi(third.c_str()));

        iReaderPos++;

        shape.material = args[iReaderPos]; iReaderPos++;
        shape.mass = (float)atof(args[iReaderPos]); iReaderPos++;

        vec.push_back(shape);
    }

    return vec;
}

xr_vector<CEditableObject::SurfaceParams> LoadSurfaces(xr_vector<LPCSTR> args, int count)
{
    xr_vector<CEditableObject::SurfaceParams> vec;

    u8 reader = 0;
    int counter = 0;
    CEditableObject::SurfaceParams surface;

    for (int i = 0; i < int(count * 4); i++)
    {
        switch (reader)
        {
        case 0:
            surface.flags = atoi(args[iReaderPos]);
            break;
        case 1:
            surface.texture = args[iReaderPos];
            break;
        case 2:
            surface.shader = args[iReaderPos];
            break;
        }

        reader++;

        if (reader >= 3) // Конец считки, записываем сюрфейс
        {
            reader = 0;
            counter++;
            vec.push_back(surface);
        }

        iReaderPos++;

        if (counter > count - 1)
            break;
    }

    return vec;
}

xr_vector<CActorTools::BatchFiles> LoadBatchFiles(xr_vector<LPCSTR> args, int count)
{
    xr_vector<CActorTools::BatchFiles> batch_files;
    if (count > 0) // Режим папок
    {
        for (int i = 0; i < count; i++)
        {
            CActorTools::BatchFiles file;
            file.source_folder = args[iReaderPos]; iReaderPos++;
            int cnt = atoi(args[iReaderPos]); iReaderPos++;

            for (int i = 0; i < cnt; i++)
            {
                file.files.push_back(args[iReaderPos]); iReaderPos++;
            }

            batch_files.push_back(file);
        }
    }
    else // Режим файлов
    {
        int cnt = atoi(args[iReaderPos]); iReaderPos++;

        CActorTools::BatchFiles file;
        file.source_folder = "null";
        for (int i = 0; i < cnt; i++)
        {
            int cnt_2 = atoi(args[iReaderPos]); iReaderPos++;
            for (int j = 0; j < cnt_2; j++)
            {
                file.files.push_back(args[iReaderPos]); iReaderPos++;
            }
        }
        batch_files.push_back(file);
    }

    return batch_files;
}

xr_vector<shared_str> LoadStringVector(xr_vector<LPCSTR> args, int count)
{
    xr_vector<shared_str> vec;

    for (int i = 0; i < count; i++)
    {
        vec.push_back(args[iReaderPos]);
        iReaderPos++;
    }

    return vec;
}

struct Textures
{
    shared_str main;
    shared_str temp;
};

xr_vector<Textures> LoadObJTexturesVector(xr_vector<LPCSTR> args, int count)
{
    xr_vector<Textures> vec;

    for (int i = 0; i < count; i++)
    {
        Textures texture;
        texture.main = args[iReaderPos]; iReaderPos++;
        texture.temp = args[iReaderPos]; iReaderPos++;
        vec.push_back(texture);
    }

    return vec;
}

bool HasAlpha(BearImage& Texture)
{
    bool alpha = false;
    FOR_START(size_t, 0, Texture.GetWidth(), w)
    {
        for (size_t h = 0; h < Texture.GetHeight(); h++)
        {
            uint8 px = Texture.GetPixel(w, h).A8U;
            if (px < 254)
                alpha = true;

            if (alpha)
            {
                tbb::task::self().cancel_group_execution();
                break;
            }
        }
    }
    FOR_END

    return alpha;
}

void ConvertDDStoPng(shared_str dds, shared_str png)
{
	BearImage Texture;
    Texture.LoadFromFile(dds.c_str());
    Texture.Convert(HasAlpha(Texture) ? BearTexturePixelFormat::R8G8B8A8 : BearTexturePixelFormat::R8G8B8);
    Texture.SaveToPng(png.c_str());
}

bool generate_commands = false;
bool load_commands = false;

int main(int argc, char** argv)
{
    Core._initialize("Actor", ELogCallback,1, "",true, true);

    if (argc > 4)
        Core.DebugLog = (atoi(argv[4]) & exfDbgWindow) || IsDebuggerPresent();

    xr_vector<LPCSTR> args;

    for (int i = 0; i < argc; i++)
        args.push_back(argv[i]);

    if (generate_commands)
    {
        std::ofstream commands;
        commands.open("debug_commands.txt"); 

        for (int i = 0; i < argc; i++)
            commands << args[i] << std::endl;
        commands.close();
    }
    else if (load_commands)
    {
        std::ifstream file("G:\\projects\\ValeroK\\xrExportTool\\bin\\x64\\Release\\debug_commands.txt");
        Msg("Start loading txt commands");

        if (file.is_open())
        {
            std::string line;
            argc = 0;
            args.clear();
            while (std::getline(file, line))
            {
                args.push_back(line.c_str());
                Msg("load %s", args.back());
                argc++;
            }
            file.close();
        }
    }

    Msg("[Arg debugger]");
    Msg("Arg count: %d", argc);
    int size = 0;
    for (int i = 0; i < argc; i++)
    {
        shared_str arg = args[i];
        size += arg.size();
    }
    Msg("Arg size: %d", size);

    if (argc == 1 && !IsDebuggerPresent())
    {
        Msg("Please, run Object tool.exe");
        system("pause");
        return 0;
    }

    // Program params
    int ret_code = 0;
    int mode;
    shared_str object_path;
    shared_str second_file_path;
    int flags = 0;
    float scale = 1.0f;
    xr_vector<CEditableObject::ShapeEditType> pShapes;
    xr_vector<CEditableObject::SurfaceParams> pSurfaces;
    xr_vector<shared_str> pLoadedAnims;
    float lod_quality = 1.0f;
    int lod_flags = 0;
    shared_str lod_path = "";
    xr_vector<shared_str> pMotionRefs;
    xr_vector<CActorTools::BatchFiles> pBatchFiles;
    shared_str batch_out = "";
    int cpp_export_mode = 0;
    shared_str custom_script = "";
    shared_str source_object = "";
    shared_str temp_path = "";
    xr_vector<Textures> pObjTextures;
    u8 model_type = u8(-1);
    // End of program params

    if (!IsDebuggerPresent() || load_commands)
    {
        // Program params
        mode = atoi(args[1]);
        object_path = args[2];
        second_file_path = args[3];
        flags = atoi(args[4]);
        scale = atof(args[5]);
        int shapes_count = atoi(args[6]);
        int surfaces_count = atoi(args[7]);
        int loaded_skls_count = atoi(args[8]);
        iReaderPos = 9;
        pShapes = LoadShapes(args, shapes_count);
        pSurfaces = LoadSurfaces(args, surfaces_count);
        pLoadedAnims = LoadStringVector(args, loaded_skls_count);
        lod_quality = atof(args[iReaderPos]); iReaderPos++;
        lod_flags = atoi(args[iReaderPos]); iReaderPos++;
        lod_path = args[iReaderPos]; iReaderPos++;
        int motion_refs_count = atoi(args[iReaderPos]); iReaderPos++;
        pMotionRefs = LoadStringVector(args, motion_refs_count);
        int batch_files_count = atoi(args[iReaderPos]); iReaderPos++;
        pBatchFiles = LoadBatchFiles(args, batch_files_count);
        batch_out = args[iReaderPos]; iReaderPos++;
        cpp_export_mode = atoi(args[iReaderPos]); iReaderPos++;
        custom_script = args[iReaderPos]; iReaderPos++;
        source_object = args[iReaderPos]; iReaderPos++;
        int obj_textures_count = atoi(args[iReaderPos]); iReaderPos++;
        pObjTextures = LoadObJTexturesVector(args, obj_textures_count);
        temp_path = args[iReaderPos]; iReaderPos++;
        model_type = atoi(args[iReaderPos]); iReaderPos++;
        // End of program params
    }
    else
    {
        // Program params
        mode = ExportOGF;
        object_path = "H:\\8.object";
        second_file_path = "H:\\8.ogf";
        //flags = exfHQGeometryPlus;
        //custom_script = "G:\\projects\\ValeroK\\xrExportTool\\bin\\x64\\Release\\scripts\\delete_unused_gunslinger_bones.script";
        // End of program params
    }

    Core.CurrentMode = mode;

    std::string line;
    std::string userdata_path = object_path.c_str();
    xr_string userdata = "";
    userdata_path += "_temp.userdata";

    std::ifstream fuserdata(userdata_path);

    if (fuserdata.is_open())
    {
        while (getline(fuserdata, line))
        {
            userdata += line.c_str();
            userdata += "\r\n";
        }

        if (userdata.size() > 2)
        {
            userdata.pop_back();
            userdata.pop_back();
        }
    }
    fuserdata.close();

    Tools = xr_new<CActorTools>();
    ATools = (CActorTools*)Tools;

    if (mode != BatchLtx && mode != BatchDialogOGF && mode != BatchDialogOMF)
    {
        if (!ATools->LoadScale(object_path.c_str(), scale, (flags & exfScaleCenterMass), source_object.c_str()))
        {
            Core._destroy();
            return -1;
        }

        ATools->CurrentObject()->ChangeSurfaceFlags(pSurfaces);
        ATools->CurrentObject()->ChangeBoneShapeTypes(pShapes);
        ATools->CurrentObject()->m_LODs = lod_path;
        ATools->CurrentObject()->GetClassScript() = userdata.c_str();
        ATools->CurrentObject()->m_EditorScript = custom_script;
        ATools->CurrentObject()->m_TempPath = temp_path;

        if (model_type != u8(-1))
            ATools->CurrentObject()->m_objectFlags.set(CEditableObject::eoDynamic, model_type == 0);

        if (!IsDebuggerPresent())
            ATools->CurrentObject()->m_SMotionRefs = pMotionRefs;

        ATools->CurrentObject()->m_objectFlags.set(CEditableObject::eoProgressive, (flags & exfMakeProgressive));
        ATools->CurrentObject()->m_objectFlags.set(CEditableObject::eoStripify, (flags & exfMakeStripify));
        ATools->CurrentObject()->m_objectFlags.set(CEditableObject::eoOptimizeSurf, (flags & exfOptimizeSurfaces));
        ATools->CurrentObject()->m_objectFlags.set(CEditableObject::eoHQExportPlus, (flags & exfHQGeometryPlus));
        ATools->CurrentObject()->m_objectFlags.set(CEditableObject::eoNormals, (flags & exfSplitNormals));
        ATools->CurrentObject()->m_objectFlags.set(CEditableObject::eoExpBuildinMots, (flags & exfExportBuildInMots));
        ATools->CurrentObject()->m_objectFlags.set(CEditableObject::eoSoCSmooth, (flags & exfSoCSmooth));
        ATools->CurrentObject()->m_objectFlags.set(CEditableObject::eoSoCInfluence, (flags & exfSoCInfluence));
        ATools->CurrentObject()->m_objectFlags.set(CEditableObject::eoAutoSmooth, (flags & exfAutoSmooth));
        ATools->CurrentObject()->InitScript();
    }

    if (flags & exfHQGeometryPlus)
        g_EpsSkelPositionDelta = EPS_S;

    if (flags & exf16Bit)
        g_force16BitTransformQuant = true;
    else if (flags & exfNoCompress)
        g_forceFloatTransformQuant = true;

    switch (mode)
    {
        case ExportOGF:
        {
            if (!ATools->ExportOGF(second_file_path.c_str()))
                ret_code = -1;
        }break;
        case ExportOMF:
        {
            ATools->CurrentObject()->m_objectFlags.set(CEditableObject::eoExpBuildinMots, TRUE);
            if (!ATools->ExportOMF(second_file_path.c_str()))
                ret_code = -1;
        }break;
        case GenerateShape:
        {
            if (!ATools->CurrentObject()->GenerateBoneShape(false) || !ATools->Save(object_path.c_str()))
                ret_code = -1;
            WriteLog("..Shapes generated");
        }break;
        case DeleteMotions:
        {
            ATools->CurrentObject()->ClearSMotions();
            if (!ATools->Save(object_path.c_str()))
                ret_code = -1;
            WriteLog("..Object saved");
        }break;
        case LoadMotions:
        {
            bool res = true;
            for (int i = 0; i < pLoadedAnims.size(); i++)
            {
                if (!ATools->AppendMotion(pLoadedAnims[i].c_str()))
                    res = false;
            }
            if (res)
                res = ATools->Save(object_path.c_str());

            if (!res)
                ret_code = -1;
            WriteLog("..Motions loaded");
        }break;
        case SaveSklsMotions:
        {
            if (!ATools->SaveMotions(second_file_path.c_str(), false))
                ret_code = -1;
            WriteLog("..Motions saved");
        }break;
        case LoadBones:
        {
            if (!ATools->LoadBoneData(second_file_path.c_str()) || !ATools->Save(object_path.c_str()))
                ret_code = -1;
            WriteLog("..Bones loaded");
        }break;
        case SaveBones:
        {
            if (!ATools->SaveBoneData(second_file_path.c_str()))
                ret_code = -1;
            WriteLog("..Bones saved");
        }break;
        case ExportOBJ:
        {
            if (!ATools->ExportOBJ(second_file_path.c_str()))
                ret_code = -1;
        }break;
        case ExportOBJOptimized:
        {
            ATools->CurrentObject()->m_objectFlags.set(CEditableObject::eoProgressive, FALSE);
            ATools->CurrentObject()->m_objectFlags.set(CEditableObject::eoOptimizeSurf, TRUE);

            for (int i = 0; i < pObjTextures.size(); i++)
                ConvertDDStoPng(pObjTextures[i].main, pObjTextures[i].temp);

            if (!ATools->ExportOBJ(second_file_path.c_str()))
                ret_code = -1;
        }break;
        case ExportDM:
        {
            if (!ATools->ExportDM(second_file_path.c_str()))
                ret_code = -1;
        }break;
        case SaveObject:
        {
            if (!ATools->Save(object_path.c_str()))
                ret_code = -1;
            WriteLog("..Object saved");
        }break;
        case LoadBoneParts:
        {
            if (!ATools->CurrentObject()->LoadBoneParts(second_file_path.c_str()) || !ATools->Save(object_path.c_str()))
                ret_code = -1;
            WriteLog("..Bone parts loaded");
        }break;
        case SaveBoneParts:
        {
            if (!ATools->CurrentObject()->SaveBoneParts(second_file_path.c_str()))
                ret_code = -1;
            WriteLog("..Bone parts saved");
        }break;
        case ToDefaultBoneParts:
        {
            if (!ATools->CurrentObject()->ToDefaultBoneParts() || !ATools->Save(object_path.c_str()))
                ret_code = -1;
            WriteLog("..Bone parts reseted to default");
        }break;
        case SaveSklMotions:
        {
            if (!ATools->SaveMotions(second_file_path.c_str(), true))
                ret_code = -1;
            WriteLog("..Motions saved");
        }break;
        case GenerateLod:
        {
            Msg("Quality: %f", lod_quality);
            float lq = 0.01f;
            float hq = 0.002f;
            float quality = lq + (lod_quality / 1.0f * (hq - lq));
            g_EpsSkelPositionDelta = quality;
            ATools->CurrentObject()->m_objectFlags.set(CEditableObject::eoOptimizeSurf, TRUE);
            ATools->CurrentObject()->m_objectFlags.set(CEditableObject::eoHQExportPlus, FALSE);
            ATools->CurrentObject()->m_objectFlags.set(CEditableObject::eoProgressive, (lod_flags & exfMakeProgressive));
            ATools->CurrentObject()->m_objectFlags.set(CEditableObject::eoStripify, FALSE); // Крашит
            ATools->CurrentObject()->Optimize();
            Msg("Lod quality: %f", g_EpsSkelPositionDelta);
            if (!ATools->ExportOGF(second_file_path.c_str()))
                ret_code = -1;
        }break;
        case SaveCpp:
        {
            if (!ATools->ExportCPP(second_file_path.c_str(), cpp_export_mode))
                ret_code = -1;
            WriteLog("..Cpp saved");
        }break;
        case BatchLtx:
        {
            if (!ATools->BatchConvert(second_file_path.c_str(), flags, custom_script, scale))
                ret_code = -1;
            WriteLog("..Batch successful");
        }break;
        case BatchDialogOGF:
        {
            if (!ATools->BatchConvertDialogOGF(pBatchFiles, batch_out, flags, custom_script, scale))
                ret_code = -1;
            WriteLog("..Batch successful");
        }break;
        case BatchDialogOMF:
        {
            if (!ATools->BatchConvertDialogOMF(pBatchFiles, batch_out, flags, custom_script, scale))
                ret_code = -1;
            WriteLog("..Batch successful");
        }break;
        case CreateMTL:
        {
            for (int i = 0; i < pObjTextures.size(); i++)
                ConvertDDStoPng(pObjTextures[i].main, pObjTextures[i].temp);

            if (!ATools->CreateMTL(second_file_path.c_str()))
                ret_code = -1;
            WriteLog("..Mtl created");
        }break;
    }

    Core._destroy();

    if (IsDebuggerPresent() || flags & exfDbgWindow)
        system("pause");

    if (ret_code != 0)
        WriteLog("..Exit with error: %d", ret_code);

    return ret_code;
}
