using System.Collections;
using System.Collections.Generic;
using System;
using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using EasyMeshVR.Core;
using Hashtable = ExitGames.Client.Photon.Hashtable;

namespace EasyMeshVR.Multiplayer
{
    public class NetworkPlayerManager : MonoBehaviourPunCallbacks
    {
        #region Public Fields

        public static NetworkPlayerManager instance { get; private set; }

        #endregion

        #region Private Fields

        [SerializeField]
        private Transform[] spawnPoints = new Transform[Constants.MAX_PLAYERS_PER_ROOM];

        [SerializeField]
        private GameObject networkPlayerPrefab;

        [SerializeField]
        private GameObject XROrigin;

        private GameObject spawnedPlayerPrefab;

        private int myPlayerNumber = 0;

        private const string PLAYER_NUMBER_PROPERTY = "playerNumber";

        #endregion

        #region MonoBehaviour Callbacks

        void Awake()
        {
            instance = this;
        }

        void OnValidate()
        {
            if (spawnPoints.Length != Constants.MAX_PLAYERS_PER_ROOM)
            {
                Debug.LogWarning("The number of spawn points should be equal to " + Constants.MAX_PLAYERS_PER_ROOM);
                Array.Resize(ref spawnPoints, Constants.MAX_PLAYERS_PER_ROOM);
            }
        }

        void Start()
        {
            spawnedPlayerPrefab = SpawnPlayer();
        }

        #endregion

        #region Pun Callbacks

        public override void OnLeftRoom()
        {
            base.OnLeftRoom();

            if (spawnedPlayerPrefab != null)
            {
                Debug.Log("Destroying player prefab");
                PhotonNetwork.Destroy(spawnedPlayerPrefab);
            }
        }

        #endregion

        #region Public Methods

        public GameObject SpawnPlayer()
        {
            Transform spawnPoint = GetNextSpawnPoint();
            XROrigin.transform.position = spawnPoint.position;
            XROrigin.transform.rotation = spawnPoint.rotation;

            return PhotonNetwork.Instantiate(networkPlayerPrefab.name, Vector3.zero, Quaternion.identity);
        }

        #endregion

        #region Private Methods

        private Transform GetNextSpawnPoint()
        {
            myPlayerNumber = 0;

            Player[] playerList = PhotonNetwork.PlayerList;
            Array.Sort(playerList, (p1, p2) =>
            {
                if (p1 == PhotonNetwork.LocalPlayer)
                {
                    return 1;
                }
                if (p2 == PhotonNetwork.LocalPlayer)
                {
                    return -1;
                }

                return (int)p1.CustomProperties[PLAYER_NUMBER_PROPERTY] - (int)p2.CustomProperties[PLAYER_NUMBER_PROPERTY];
            });

            foreach (Player p in playerList)
            {
                if (p == PhotonNetwork.LocalPlayer) continue;

                Hashtable customProperties = p.CustomProperties;

                int otherPlayerNumber = (int)customProperties[PLAYER_NUMBER_PROPERTY];

                if (myPlayerNumber < otherPlayerNumber || myPlayerNumber > otherPlayerNumber)
                {
                    break;
                }

                myPlayerNumber++;
            }

            Player myPlayer = PhotonNetwork.LocalPlayer;
            Hashtable myProperties = new Hashtable();
            myProperties.Add(PLAYER_NUMBER_PROPERTY, myPlayerNumber);
            myPlayer.SetCustomProperties(myProperties);

            Debug.LogFormat("Assigned playerNumber {0} to local player", myPlayerNumber);

            if (myPlayerNumber < 0 || myPlayerNumber >= Constants.MAX_PLAYERS_PER_ROOM)
            {
                Debug.LogError("Failed to find a valid spawn point for player");
                return null;
            }

            return spawnPoints[myPlayerNumber];
        }

        #endregion
    }
}
