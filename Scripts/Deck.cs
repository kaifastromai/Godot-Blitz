#nullable enable
using Godot;
using System;
using System.Collections.Generic;
using System.Reflection.Metadata;
using Blitz.Scripts;
public enum DeckType{
    Pile,
        PlayerHand,
}
public partial class Deck : Area2D
{
    public  const float CardYXRatio = 1.34f;
    [Export] public Proto.Color Color;


    private int _cardCount;
    private Sprite2D[]? _cardSprites;
    private ShaderMaterial? _cardMaterial;
    private float _cardRenderSize = 600;
    private DeckType _deckType = DeckType.Pile;

    public Deck(DeckType deckType=DeckType.Pile)
    {
        _deckType = deckType;
    }

    public float CardRenderSize
    {
        get => _cardRenderSize;
        set
        {
            _cardRenderSize = value;
            if (_cardSprites != null)
                foreach (var sprite2D in _cardSprites)
                {
                    var coefx = _cardRenderSize / sprite2D.Texture.GetSize().X;
                    var coefy = _cardRenderSize / sprite2D.Texture.GetSize().Y*CardYXRatio;
                    sprite2D.Scale = new Vector2(coefx, coefy);
                }
        }
    }

    [Export]
    public int CardCount
    {
        get => _cardCount;
        set
        {
            //clamp value between 0 and 10
            value = _deckType switch
            {
                DeckType.Pile => Math.Clamp(value, 0, 10),
                DeckType.PlayerHand => Mathf.Clamp(value, 0, 40),
                _ => throw new ArgumentOutOfRangeException()
            };
            _cardCount = value;
            renderCards();
        }
    }

    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
        var rng = new RandomNumberGenerator();
        _cardSprites = new Sprite2D[10];
        var colorIndex = Color switch
        {
            Proto.Color.Red => 0,
            Proto.Color.Green => 1,
            Proto.Color.Blue => 2,
            Proto.Color.Yellow => 3,
            _ => 0
        };
        //load shader from Shaders/card_shader.tres
        _cardMaterial = GD.Load<ShaderMaterial>("res://Shaders/card_shader.tres");

        if (_deckType==DeckType.Pile)
        {

            //load all 
            for (int i = 0; i < 10; i++)
            {
                var texture2D = GD.Load<Texture2D>($"res://assets/svgs/blitz_card_{colorIndex * 10 + i + 1}.svg");
                var sprite2D = new Sprite2D();
                sprite2D.Texture = texture2D;
                var pos = Utils.AroundVec2(Vector2.Zero, 0);
                sprite2D.Position = new Vector2(0, 0) + pos;
                sprite2D.Visible = false;
                sprite2D.Material = _cardMaterial.Duplicate() as ShaderMaterial;
                var randomAngle = rng.RandfRange(-1, 1) * Mathf.Pi / 8;
                sprite2D.Rotation = randomAngle;
                var coefx = _cardRenderSize / sprite2D.Texture.GetSize().X;
                var coefy = _cardRenderSize / sprite2D.Texture.GetSize().Y*CardYXRatio;
                sprite2D.Scale = new Vector2(coefx, coefy);

                //set the sat uniform to a decreasing value
                if (sprite2D.Material != null)
                    ((ShaderMaterial)sprite2D.Material).SetShaderParameter("sat", 0.5 + (1 + i) * 0.05f);
                AddChild(sprite2D);
                _cardSprites[i] = sprite2D;
            }
        }
        else
        {
            for (int i = 0; i < 40; i++)
            {
                var texture = GD.Load<Texture2D>("res://assets/svgs/blitz_card_face.svg");
                var sprite = new Sprite2D();
                sprite.Texture = texture;
                var ifloat = (float)i;
                var pos = new Vector2(ifloat / 40, ifloat / 40) * 5;
                
                var randomAngle = rng.RandfRange(-1, 1) * Mathf.Pi / 16;
                sprite.Rotation = randomAngle;
                sprite.Position = pos;
                var coefx = _cardRenderSize / sprite.Texture.GetSize().X;
                var coefy = _cardRenderSize / sprite.Texture.GetSize().Y * CardYXRatio;
                sprite.Scale = new Vector2(coefx, coefy);
                if (sprite.Material != null)
                {
                    ((ShaderMaterial)sprite.Material).SetShaderParameter("sat",0.5+(1+ifloat)/80);
                }
                AddChild(sprite);
                _cardSprites[i] = sprite;
            }
            

        }

        renderCards();
        //hello
    }

    private void renderCards()
    {
        if (_cardSprites == null) return;
        //set all cards greater than card count to invisible
        for (int i = 0; i < 10; i++)
        {
            _cardSprites[i].Visible = i < _cardCount;
        }
    }

    // Called every frame. 'delta' is the elapsed time since the previous frame.
    public override void _Process(double delta)
    {
    }
}