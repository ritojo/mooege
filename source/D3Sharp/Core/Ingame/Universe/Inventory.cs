﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using D3Sharp.Net.Game.Message.Definitions;
using D3Sharp.Net.Game.Message.Fields;
using D3Sharp.Net.Game;
using D3Sharp.Net.Game.Message.Definitions.ACD;
using D3Sharp.Net.Game.Message.Definitions.Misc;
using D3Sharp.Net.Game.Message.Definitions.Attribute;
using D3Sharp.Net.Game.Message.Definitions.Inventory;
using D3Sharp.Net.Game.Message.Definitions.Combat;
using D3Sharp.Core.Common.Items;
using D3Sharp.Core.Ingame.Actors;
using D3Sharp.Net.Game.Message;
using D3Sharp.Utils;

namespace D3Sharp.Core.Ingame.Universe
{
    // Items are stored for this moment in GameClient, 
    // this shold be esier way to generate specific or random item by any player...
    // Putting all game items outside and place in some class in future schuld make esier way to load and save to database
    
    // Backpack is organized by adding an item to EVERY slot it fills
    public class Inventory:IMessageConsumer
    {
        static readonly Logger Logger = LogManager.CreateLogger();

        public int Rows { get { return backpack.GetLength(0); } }
        public int Columns { get { return backpack.GetLength(1); } }
        public int EquipmentSlots { get { return equipment.GetLength(0); } }
        
        private int[] equipment;      // array of equiped items_id  (not item)
        private int[,] backpack;      // backpack array

        private Hero owner; // Used, because most information is not in the item class but Actors managed by the world

        public struct InventorySize
        {
            public int Width;
            public int Height;
        }
        private struct InventorySlot
        {
            public int Row;
            public int Column;
        }

        // This should be in the database#
        // Do all items need a rectangual space in diablo 3?
        private InventorySize GetItemInventorySize(int itemID)
        {
            //Actor actor = owner.CurrentWorld.GetActor(owner.InGameClient.items[itemID].Gbid);
            
            //if (actor.SnoId == 4440) return new InventorySize() { Width = 1, Height = 1 }; // minor health potion
            //if (actor.SnoId == 3245) return new InventorySize() { Width = 1, Height = 2 }; // hand axe 1


            return new InventorySize() { Width = 1, Height = 2 };
        }

        private bool FreeSpace(int droppedItemID, int row, int column)
        {
             InventorySize size = GetItemInventorySize(droppedItemID);

             for (int r = row; r < Math.Min(row + size.Height, Rows); r++)
                 for (int c = column; c < Math.Min(column + size.Width, Columns); c++)
                  if(backpack[r,c]!=0)
                     return false;
             return true;
        }

        /// <summary>
        /// Collects (counts) the items overlapping with the item about to be dropped.
        /// If there are none, drop item
        /// If there is exacly one, swap it with item (TODO)
        /// If there are more, item cannot be dropped
        /// </summary>
        private int collectOverlappingItems(int droppedItemID, int row, int column)
        {
            InventorySize dropSize = GetItemInventorySize(droppedItemID);
            List<int> overlapping = new List<int>();

            // For every slot...
            for (int r = row; r < Rows; r++)
                for (int c = 0; c < Columns; c++)

                    // that contains an item other than the one we want to drop
                    if (backpack[r,c] != 0 && backpack[r, c] != droppedItemID)  //TODO this would break for an item with id 0

                        // add it to the list if if dropping the item in <row, column> would need the same slot
                        if (r >= row && r <= row + dropSize.Height)
                            if (c >= column && c <= column + dropSize.Width)
                                if (!overlapping.Contains(backpack[r, c]))
                                    overlapping.Add(backpack[r, c]);

            return overlapping.Count;
        }

        /// <summary>
        /// Removes and item from the backpack
        /// </summary>
        private void RemoveItem(int itemID)
        {
            for (int r = 0; r < Rows; r++)
                for (int c = 0; c < Columns; c++)
                    if (backpack[r, c] == itemID)
                        backpack[r, c] = 0;
        }

        /// <summary>
        /// Adds an item to the backpack
        /// </summary>
        void AddItem(int itemID, int row, int column)
        {
            InventorySize size = GetItemInventorySize(itemID);

            //check backback boundaries
            if (row + size.Width > Rows || column + size.Width > Columns) return;

            for (int r = row; r < Math.Min(row + size.Height, Rows); r++)
                for (int c = column; c < Math.Min(column + size.Width, Columns); c++)
                {
                    System.Diagnostics.Debug.Assert(backpack[r, c] == 0, "You need to remove an item from the backpack before placing another item there");
                    backpack[r, c] = itemID;
                }
        }

        /// <summary>
        /// Refreshes the visual appearance of the hero
        /// TODO: this should go to hero class
        /// </summary>
        /// <param name="PlayerID"></param>
        void RefreshVisual(int PlayerID)
        {
            owner.InGameClient.SendMessage(new VisualInventoryMessage()
            {
                Id = (int)Opcodes.VisualInventoryMessage,
                Field0 = PlayerID,
                Field1 = new VisualEquipment()
                {
                    Field0 = new VisualItem[8]
                    {
                        owner.InGameClient.items[equipment[0]].CreateVisualItem(),
                        owner.InGameClient.items[equipment[1]].CreateVisualItem(),
                        owner.InGameClient.items[equipment[2]].CreateVisualItem(),
                        owner.InGameClient.items[equipment[3]].CreateVisualItem(),
                        owner.InGameClient.items[equipment[4]].CreateVisualItem(),
                        owner.InGameClient.items[equipment[5]].CreateVisualItem(),
                        owner.InGameClient.items[equipment[6]].CreateVisualItem(),
                        owner.InGameClient.items[equipment[7]].CreateVisualItem(),
                    },
                },
            });

            // Finalize
            // TODO find out if that is necessary
            owner.InGameClient.PacketId += 10 * 2;
            owner.InGameClient.SendMessage(new DWordDataMessage()
            {
                Id = 0x89,
                Field0 = owner.InGameClient.PacketId,
            });

            owner.InGameClient.FlushOutgoingBuffer();

        }

        /// <summary>
        /// Equips an item in an equipment slote
        /// </summary>
        void EquipItem(int itemID, int slot)
        {
            equipment[slot] = itemID;  
        }

        /// <summary>
        /// Removes an item from the equipment slot it uses
        /// </summary>
        void UnequipItem(int itemID)
        {
            for (int i = 0; i < EquipmentSlots; i++)
                if (equipment[i] == itemID)
                    equipment[i] = 0;
        }

        void AcceptMoveRequest(InventoryRequestMoveMessage request)
        {
            owner.InGameClient.SendMessage(new ACDInventoryPositionMessage()
            {
                Id = (int)Opcodes.ACDInventoryPositionMessage,
                Field0 = request.Field0,    // ItemID
                Field1 = new InventoryLocationMessageData()
                {
                    Field0 = request.Field1.Field0, // Inventory Owner
                    Field1 = request.Field1.Field1, // EquipmentSlot
                    Field2 = new IVector2D()
                    {
                        Field0 = request.Field1.Field2, // Row
                        Field1 = request.Field1.Field3, // Column
                    },
                },
                Field2 = 1 // what does this do?  // 0- source item not disappearing from inventory, 1 - Moving, any other possibilities? its an int32
            });

            owner.InGameClient.PacketId += 10 * 2;
            owner.InGameClient.SendMessage(new DWordDataMessage()
            {
                Id = 0x89,
                Field0 = owner.InGameClient.PacketId,
            });

            owner.InGameClient.FlushOutgoingBuffer();
        }

        public Inventory(Hero owner)
        {
            this.owner = owner;
            backpack = new int[6, 10];
            equipment = new int[8];

            // TODO this is for testing. When using ACDEnterKnown to place items in inventory, make sure to add them here as well
            AddItem(0x789E01f2, 0, 0);
            AddItem(0x789E01f7, 0, 2);
        }

        /// <summary>
        /// Returns whether an item is equipped
        /// </summary>
        Boolean isItemEquipped(int itemID)
        {
            for (int i = 0; i < EquipmentSlots; i++)
                if (equipment[i] == itemID)
                    return true;
            return false;
        }

        /// <summary>
        /// Checks wheter the inventory contains an item
        /// </summary>
        public bool Contains(int itemId)
        {
            for (int r = 0; r < Rows; r++)
                for (int c = 0; c < Columns; c++)
                    if (backpack[r, c] == itemId)
                        return true;
            return false;
        }

        /// <summary>
        /// Find an inventory slot with enough space for an item
        /// </summary>
        /// <returns>Slot or null if there is no space in the backpack</returns>
        private InventorySlot? FindSlotForItem(int itemID)
        {
            InventorySize size = GetItemInventorySize(itemID);

            for (int r = 0; r < Rows - size.Width + 1; r++)
                for (int c = 0; c < Columns - size.Height + 1; c++)
                    if (collectOverlappingItems(itemID, r, c) == 0)
                        return new InventorySlot() { Row = r, Column = c };
            return null;
        }

        /// <summary>
        /// Picks an item up after client request
        /// </summary>
        public void PickUp(TargetMessage msg)
        {
            System.Diagnostics.Debug.Assert(!Contains(msg.Field1) && !isItemEquipped(msg.Field1), "Item already in inventory");
            // TODO Ensure target is an item and it exists
            
            // TODO Autoequip when equipment slot is empty
            
            InventorySlot? freeSlot = FindSlotForItem(msg.Field1);
            if (freeSlot == null)
            {
                //Inventory full
                owner.InGameClient.SendMessage(new ACDPickupFailedMessage()
                {
                    Id = (int)Opcodes.ACDPickupFailedMessage,
                    ItemId = msg.Field1,
                    Reason = ACDPickupFailedMessage.Reasons.InventoryFull
                });
            }
            else
            {
                AddItem(msg.Field1, freeSlot.Value.Row, freeSlot.Value.Column);

                owner.InGameClient.SendMessage(new ACDInventoryPositionMessage()
                {
                    Id = (int)Opcodes.ACDInventoryPositionMessage, 
                    Field0 = msg.Field1,    // ItemID
                    Field1 = new InventoryLocationMessageData()
                    {
                        Field0 = owner.Id, // Inventory Owner
                        Field1 = 0x00000000, // EquipmentSlot
                        Field2 = new IVector2D()
                        {
                            Field0 = freeSlot.Value.Column,
                            Field1 = freeSlot.Value.Row
                        },
                    },
                    Field2 = 1  // TODO, find out what this is and why it must be 1...is it an enum?
                });
            }

            // Finalize
            // TODO find out if that is necessary
            owner.InGameClient.PacketId += 10 * 2;
            owner.InGameClient.SendMessage(new DWordDataMessage()
            {
                Id = 0x89,
                Field0 = owner.InGameClient.PacketId,
            });

            owner.InGameClient.FlushOutgoingBuffer();
        }

        /// <summary>
        /// Handles a request to move an item within the inventory.
        /// This covers moving items within the backpack, from equipment
        /// slot to backpack and from backpack to equipment slot
        /// </summary>
        public void HandleInventoryRequestMoveMessage(InventoryRequestMoveMessage request)
        {
            // Request to equip item from backpack
            if (request.Field1.Field1 != 0)
            {
                System.Diagnostics.Debug.Assert(Contains(request.Field0) || isItemEquipped(request.Field0), "Request to equip unknown item");

                // TODO find out swapping items, so no equipping when the slot is occupied
                if (request.Field1.Field1 < EquipmentSlots && this.equipment[request.Field1.Field1] == 0)
                {
                    Logger.Debug("Equip Item {0}", request.AsText());
                    RemoveItem(request.Field0);
                    EquipItem(request.Field0, request.Field1.Field1);
                    AcceptMoveRequest(request);
                    RefreshVisual(request.Field1.Field0);
                }
            }

            // Request to move an item (from backpack or equipmentslot)
            else
            {
                if (FreeSpace(request.Field0, request.Field1.Field3, request.Field1.Field2))
                {
                    if (isItemEquipped(request.Field0))
                    {
                        Logger.Debug("Unequip item {0}", request.AsText());
                        UnequipItem(request.Field0);
                        RefreshVisual(request.Field1.Field0);
                    }
                    else
                    {
                        RemoveItem(request.Field0);
                    }
                    AddItem(request.Field0, request.Field1.Field3, request.Field1.Field2);
                    AcceptMoveRequest(request);
                }
            }
        }

        public void OnInventorySplitStackMessage(InventorySplitStackMessage msg)
        {
            // TODO need to create and introduce a new item that is of the same type as the source   
        }

        /// <summary>
        /// Transfers an amount from one stack to another
        /// </summary>
        public void OnInventoryStackTransferMessage(InventoryStackTransferMessage msg)
        {
            owner.InGameClient.items[msg.Field0].Count = owner.InGameClient.items[msg.Field0].Count - (int)msg.Field2;
            owner.InGameClient.items[msg.Field1].Count = owner.InGameClient.items[msg.Field1].Count + (int)msg.Field2;
            
            // Update source
            owner.InGameClient.SendMessage(new AttributeSetValueMessage
            {
                Id = (int)Opcodes.AttributeSetValueMessage,
                Field0 = msg.Field0,
                Field1 = new NetAttributeKeyValue
                {
                    Attribute = GameAttribute.Attributes[0x0121],       // ItemStackQuantityLo 
                    Int = owner.InGameClient.items[msg.Field0].Count,   // quantity
                    Float = 0f,
                }
            });

            // Update target
            owner.InGameClient.SendMessage(new AttributeSetValueMessage
            {
                Id = 0x4c,
                Field0 = msg.Field1,
                Field1 = new NetAttributeKeyValue
                {
                    Attribute = GameAttribute.Attributes[0x0121],       // ItemStackQuantityLo 
                    Int = owner.InGameClient.items[msg.Field1].Count,   // count
                    Float = 0f,
                }
            });

            owner.InGameClient.PacketId += 10 * 2;
            owner.InGameClient.SendMessage(new DWordDataMessage()
            {
                Id = 0x89,
                Field0 = owner.InGameClient.PacketId,
            }); 
        }

        public void Consume(GameClient client, GameMessage message)
        {
            if (message is InventoryRequestMoveMessage) HandleInventoryRequestMoveMessage(message as InventoryRequestMoveMessage);
            else if (message is InventorySplitStackMessage) OnInventorySplitStackMessage(message as InventorySplitStackMessage);
            else if (message is InventoryStackTransferMessage) OnInventoryStackTransferMessage(message as InventoryStackTransferMessage);
            else return;
        }
    }
}
