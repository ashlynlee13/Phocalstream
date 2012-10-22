﻿using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;

namespace Phocalstream_Web.Models.Entity
{
    public class Collection
    {
        [Key]
        public long ID { get; set; }
        public string Name { get; set; }
        public virtual CameraSite Site { get; set; }
        public User Owner { get; set; }
        public ICollection<Photo> Photos { get; set; }

        // if the type is SITE then the Photos collection will be empty
        public CollectionType Type { get; set; }
        public CollectionStatus Status { get; set; }
    }

    public enum CollectionType
    {
        SITE,
        USER
    }

    public enum CollectionStatus
    {
        PROCESSING, 
        COMPLETE
    }
}