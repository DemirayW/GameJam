using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class Checkpoint : MonoBehaviour
{
    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Player"))
        {
            CheckpointManager.SetCheckpoint(transform.position);
            
            // Dokunulduğunda oyuncuya görsel geri bildirim (Örneğin yeşile dönme)
            SpriteRenderer sr = GetComponent<SpriteRenderer>();
            if (sr != null) sr.color = Color.green; 
            
            // Aynı checkpointi defalarca almamak için collider'ı kapatıyoruz
            GetComponent<Collider2D>().enabled = false;
        }
    }
}
