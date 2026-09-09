using System;
using System.Collections.Generic;
using KingdomCollapse.Core;
using UnityEngine;

namespace KingdomCollapse.Game
{
    /// <summary>
    /// Base dos assets de conteudo. Cada asset e um espelho editavel de um tipo puro
    /// do Core: o Inspector edita isto, o jogo roda aquilo. E o que permite balancear
    /// sem recompilar (design D3) sem que o Core conheca o Unity (design D1).
    /// </summary>
    public abstract class ContentAsset : ScriptableObject
    {
        [Tooltip("Deixe vazio para usar o nome do arquivo. Preencha so para fixar um id " +
                 "diferente do nome. Nao mudar depois que o conteudo estiver em uso: " +
                 "saves e nos de meta apontam para ele.")]
        [SerializeField] private string _id;

        /// <summary>
        /// Id vazio cai no nome do arquivo. Deliberadamente sem auto-preenchimento no
        /// OnValidate: na criacao o asset ainda tem o nome padrao do template, entao
        /// preencher ali gravaria o mesmo id em todo conteudo do mesmo tipo.
        /// </summary>
        public string Id => string.IsNullOrEmpty(_id) ? name : _id;
    }
}
