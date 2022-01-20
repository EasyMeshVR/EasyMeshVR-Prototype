using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace EasyMeshVR.UI
{
    public class GameMenuManager : MonoBehaviour
    {
        #region Private Fields

        [SerializeField]
        GameMenu gameMenu;

        [SerializeField]
        InputActionReference toggleGameMenuRef;

        #endregion

        #region Public Methods

        #endregion

        #region MonoBehaviour Callbacks

        void Awake()
        {
            toggleGameMenuRef.action.started += ToggleGameMenuAction;
        }

        void OnDestroy()
        {
            toggleGameMenuRef.action.started -= ToggleGameMenuAction;
        }

        void Start()
        {
            gameMenu.gameObject.SetActive(false);
        }

        #endregion

        #region Action Callbacks

        private void ToggleGameMenuAction(InputAction.CallbackContext context)
        {
            if (!IsLauncherActiveScene())
            {
                gameMenu.gameObject.SetActive(!gameMenu.gameObject.activeInHierarchy);
            }
        }

        #endregion

        #region Private Methods

        private bool IsLauncherActiveScene()
        {
            return SceneManager.GetActiveScene().buildIndex == 0;
        }

        #endregion
    }
}