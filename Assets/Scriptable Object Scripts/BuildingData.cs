using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[CreateAssetMenu(fileName = "BuildingData", menuName = "ScriptableObjects/BuildingData")]
public class BuildingData : ScriptableObject
{
    public enum BuildingType
    {
        Storage,
        TestCube
    }
    //Used when you have building that have subtypes (like storage which can be wood, stone or food, so it uses Material BuildingSubtype)
    public enum BuildingSubtypeStructure
    {
        None,
        Material, 
        ItemType
    }

    [Serializable]
    public class Building
    {
        public BuildingType type;
        public List<string> subtypeNames;
        public List<GameObject> baseObjects; //should be in order of subtypeNames
        public GameObject buildingModeObject;
        public Sprite buildingSprite;
    }

    public List<Building> buildingDataList = new();

    public Building GetDataOfBuildingType(BuildingType buildingType)
    {
        return buildingDataList[buildingDataList.FindIndex(building => building.type == buildingType)];
    }

}
