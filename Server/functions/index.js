/**
 * PANGAEA Cloud Functions
 * Server-authoritative validation for combat, trading, and anti-cheat
 */

const functions = require('firebase-functions');
const admin = require('firebase-admin');

admin.initializeApp();
const db = admin.firestore();

// ============================================
// COMBAT VALIDATION
// ============================================

/**
 * Validates and processes combat damage between players
 * Prevents cheating by validating on server
 */
exports.processCombatDamage = functions.https.onCall(async (data, context) => {
  // Require authentication
  if (!context.auth) {
    throw new functions.https.HttpsError('unauthenticated', 'Must be logged in');
  }

  const { targetId, damage, weaponId, isCritical } = data;
  const attackerId = context.auth.uid;

  // Validate inputs
  if (!targetId || typeof damage !== 'number') {
    throw new functions.https.HttpsError('invalid-argument', 'Invalid combat data');
  }

  // Get attacker and target data
  const [attackerDoc, targetDoc] = await Promise.all([
    db.collection('players').doc(attackerId).get(),
    db.collection('players').doc(targetId).get()
  ]);

  if (!attackerDoc.exists || !targetDoc.exists) {
    throw new functions.https.HttpsError('not-found', 'Player not found');
  }

  const attacker = attackerDoc.data();
  const target = targetDoc.data();

  // Validate damage isn't too high (anti-cheat)
  const maxPossibleDamage = calculateMaxDamage(attacker, weaponId);
  if (damage > maxPossibleDamage * 1.5) {
    console.warn(`Suspicious damage: ${attackerId} dealt ${damage} (max: ${maxPossibleDamage})`);
    throw new functions.https.HttpsError('invalid-argument', 'Invalid damage value');
  }

  // Apply karma change for PvP
  let karmaChange = -50; // Base penalty for attacking

  // Killing lower levels = more penalty
  const levelDiff = attacker.level - target.level;
  if (levelDiff > 2) karmaChange -= 25;

  // Killing bandits = less penalty
  if (target.karma < -100) karmaChange += 75;

  // Update attacker karma
  await db.collection('players').doc(attackerId).update({
    karma: admin.firestore.FieldValue.increment(karmaChange)
  });

  // Update target (if this was a kill, handled separately)

  return {
    success: true,
    validatedDamage: Math.min(damage, maxPossibleDamage),
    karmaChange
  };
});

/**
 * Calculates maximum possible damage for a player
 */
function calculateMaxDamage(player, weaponId) {
  const baseStrength = player.stats?.strength || 1;
  const baseAgility = player.stats?.agility || 1;

  // Base damage + stat bonuses
  let maxDamage = 10 + (baseStrength * 2) + (baseAgility * 1.5);

  // Weapon bonus (would look up weapon data)
  maxDamage += 20; // Placeholder

  // Critical multiplier
  maxDamage *= 2;

  return maxDamage;
}

// ============================================
// BOUNTY SYSTEM
// ============================================

/**
 * Claims a bounty when a player is killed
 */
exports.claimBounty = functions.https.onCall(async (data, context) => {
  if (!context.auth) {
    throw new functions.https.HttpsError('unauthenticated', 'Must be logged in');
  }

  const { targetId } = data;
  const claimerId = context.auth.uid;

  // Get bounty document
  const bountyDoc = await db.collection('bounties').doc(targetId).get();

  if (!bountyDoc.exists) {
    return { success: false, message: 'No bounty on this player' };
  }

  const bounty = bountyDoc.data();
  const amount = bounty.totalAmount;

  // Transfer bounty to claimer (would add to inventory/currency)
  // For now, just record it
  await db.collection('players').doc(claimerId).update({
    karma: admin.firestore.FieldValue.increment(25) // Karma boost for justice
  });

  // Clear target's bounty
  await db.collection('players').doc(targetId).update({
    bounty: 0
  });

  // Delete bounty document
  await db.collection('bounties').doc(targetId).delete();

  console.log(`Bounty claimed: ${claimerId} claimed ${amount} on ${targetId}`);

  return {
    success: true,
    amount,
    message: `Claimed ${amount} gold bounty!`
  };
});

/**
 * Adds to a player's bounty
 */
exports.placeBounty = functions.https.onCall(async (data, context) => {
  if (!context.auth) {
    throw new functions.https.HttpsError('unauthenticated', 'Must be logged in');
  }

  const { targetId, amount, reason } = data;
  const placerId = context.auth.uid;

  if (!targetId || !amount || amount < 10 || amount > 10000) {
    throw new functions.https.HttpsError('invalid-argument', 'Invalid bounty amount');
  }

  // Add or update bounty
  const bountyRef = db.collection('bounties').doc(targetId);
  const bountyDoc = await bountyRef.get();

  if (bountyDoc.exists) {
    await bountyRef.update({
      totalAmount: admin.firestore.FieldValue.increment(amount),
      contributors: admin.firestore.FieldValue.arrayUnion({
        placerId,
        amount,
        reason: reason || '',
        timestamp: admin.firestore.Timestamp.now()
      })
    });
  } else {
    await bountyRef.set({
      targetId,
      totalAmount: amount,
      createdAt: admin.firestore.Timestamp.now(),
      contributors: [{
        placerId,
        amount,
        reason: reason || '',
        timestamp: admin.firestore.Timestamp.now()
      }]
    });
  }

  // Update target's bounty field
  await db.collection('players').doc(targetId).update({
    bounty: admin.firestore.FieldValue.increment(amount)
  });

  return { success: true, message: `Placed ${amount} gold bounty` };
});

// ============================================
// WORLD EVENTS
// ============================================

/**
 * Scheduled function to spawn world events
 * Runs every 10 minutes
 */
exports.spawnWorldEvent = functions.pubsub
  .schedule('every 10 minutes')
  .onRun(async (context) => {
    const eventTypes = ['MeteorStrike', 'SupplyDrop', 'ResourceSurge', 'BossSpawn'];
    const eventType = eventTypes[Math.floor(Math.random() * eventTypes.length)];

    // Random location (would be based on player density in production)
    const position = {
      x: Math.random() * 10000 - 5000,
      y: 0,
      z: Math.random() * 10000 - 5000
    };

    const event = {
      type: eventType,
      position,
      state: 'Announced',
      announcedAt: admin.firestore.Timestamp.now(),
      startTime: admin.firestore.Timestamp.fromMillis(Date.now() + 60000), // 1 min warning
      duration: 600, // 10 minutes
    };

    await db.collection('worldEvents').add(event);
    console.log(`Spawned world event: ${eventType} at ${JSON.stringify(position)}`);

    return null;
  });

// ============================================
// ANTI-CHEAT
// ============================================

/**
 * Validates player position updates
 * Detects teleportation/speed hacks
 */
exports.validatePosition = functions.https.onCall(async (data, context) => {
  if (!context.auth) {
    throw new functions.https.HttpsError('unauthenticated', 'Must be logged in');
  }

  const { position, timestamp } = data;
  const playerId = context.auth.uid;

  // Get last known position
  const playerDoc = await db.collection('playerPositions').doc(playerId).get();

  if (playerDoc.exists) {
    const lastPosition = playerDoc.data();
    const distance = calculateDistance(lastPosition, position);
    const timeDelta = (timestamp - lastPosition.timestamp) / 1000; // seconds

    // Max speed = 15 units/second (running + mounts)
    const maxPossibleDistance = timeDelta * 15;

    if (distance > maxPossibleDistance * 1.5) {
      console.warn(`Speed hack detected: ${playerId} moved ${distance} in ${timeDelta}s`);
      // Could ban or flag player
      return { valid: false, reason: 'Invalid movement speed' };
    }
  }

  // Update position
  await db.collection('playerPositions').doc(playerId).set({
    x: position.x,
    y: position.y,
    z: position.z,
    timestamp
  });

  return { valid: true };
});

function calculateDistance(pos1, pos2) {
  const dx = pos2.x - pos1.x;
  const dy = pos2.y - pos1.y;
  const dz = pos2.z - pos1.z;
  return Math.sqrt(dx * dx + dy * dy + dz * dz);
}

// ============================================
// CLAN MANAGEMENT
// ============================================

/**
 * Handles clan invites and joins
 */
exports.joinClan = functions.https.onCall(async (data, context) => {
  if (!context.auth) {
    throw new functions.https.HttpsError('unauthenticated', 'Must be logged in');
  }

  const { clanId } = data;
  const playerId = context.auth.uid;

  // Get clan
  const clanDoc = await db.collection('clans').doc(clanId).get();
  if (!clanDoc.exists) {
    throw new functions.https.HttpsError('not-found', 'Clan not found');
  }

  const clan = clanDoc.data();

  // Check member limit (20)
  if (clan.members && clan.members.length >= 20) {
    throw new functions.https.HttpsError('resource-exhausted', 'Clan is full');
  }

  // Add player to clan
  await db.collection('clans').doc(clanId).update({
    members: admin.firestore.FieldValue.arrayUnion(playerId)
  });

  // Update player's clan reference
  await db.collection('players').doc(playerId).update({
    clanId
  });

  return { success: true, message: `Joined clan: ${clan.name}` };
});
