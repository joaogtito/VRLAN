using Unity.Netcode.Components;

namespace VRLAN.Network
{
    // NGO 1.x (Unity 2022) NÃO tem "Authority Mode = Owner" no NetworkTransform.
    // Esta subclasse força a autoridade do DONO: o cliente que possui o objeto controla a pose
    // (essencial pro avatar do aluno — head/hands — e pros grabbables que mudam de dono no grab).
    //
    // USO (só no Unity 2022 / NGO 1.x): nos prefabs, trocar o componente NetworkTransform por
    // OwnerNetworkTransform nos objetos que devem ter autoridade do dono.
    //
    // Este arquivo vive num assembly à parte (VRLAN.Network.Ngo1) que só compila quando o
    // NGO 1.x está presente (defineConstraint VRLAN_NGO1) e que referencia Unity.Netcode.Components
    // (onde o NetworkTransform mora no NGO 1.x). No NGO 2.x o assembly inteiro é excluído — lá se usa
    // o NetworkTransform padrão com AuthorityMode = Owner no Inspector.
    public class OwnerNetworkTransform : NetworkTransform
    {
        protected override bool OnIsServerAuthoritative() => false;
    }
}
